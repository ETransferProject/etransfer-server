using AElf.Contracts.MultiToken;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.TokenAccess;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenLiquidityTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenLiquidityTimerGrain : Grain<TokenLiquidityState>, ITokenLiquidityTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly ITokenAccessAppService _tokenAccessAppService;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IContractProvider _contractProvider;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    private const int PageSize = 1000;
    
    public TokenLiquidityTimerGrain(ITokenAccessAppService tokenAccessAppService, 
        ICoBoProvider coBoProvider,
        IContractProvider contractProvider,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _tokenAccessAppService = tokenAccessAppService;
        _coBoProvider = coBoProvider;
        _contractProvider = contractProvider;
        _timerOptions = timerOptions;
        _tokenAccessOptions = tokenAccessOptions;
        _withdrawOptions = withdrawOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenLiquidityTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenLiquidityTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;
        var skipCount = 0;
        var tokenApplyList = new List<TokenApplyDto>();

        while (true)
        {
            var resultDto = new PagedResultDto<TokenApplyOrderResultDto>();
            try
            {
                resultDto = await _tokenAccessAppService.GetTokenApplyListAsync(
                    new GetTokenApplyOrderListInput
                    {
                        Status = TokenApplyOrderStatus.Complete.ToString(),
                        SkipCount = skipCount,
                        MaxResultCount = PageSize
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get complete token apply order list error.");
            }
        
            if (resultDto == null || resultDto.Items == null || resultDto.Items.Count == 0)
            {
                break;
            }
        
            _logger.LogDebug("TokenLiquidityTimerGrain call, {skipCount}", skipCount);
            skipCount += resultDto.Items.Count;
            foreach (var item in resultDto.Items)
            {
                if (item.OtherChainTokenInfo != null)
                {
                    var chainId = item.OtherChainTokenInfo.ChainId;
                    tokenApplyList = AddTokenApplyList(tokenApplyList, item.Symbol, item.UserAddress, null, chainId);
                    if (!item.ChainTokenInfo.IsNullOrEmpty())
                    {
                        foreach (var chain in item.ChainTokenInfo)
                        {
                            chainId = chain.ChainId;
                            if (!_withdrawOptions.Value.PaymentAddresses?.ContainsKey(chainId) ?? false) continue;
                            var poolAddress = _withdrawOptions.Value.PaymentAddresses.GetValueOrDefault(chainId)?.GetValueOrDefault(item.Symbol);
                            if (poolAddress.IsNullOrEmpty()) continue;
                            tokenApplyList = AddTokenApplyList(tokenApplyList, item.Symbol, item.UserAddress, poolAddress, chainId);
                        }
                    }
                }
                if (item.OtherChainTokenInfo == null && !item.ChainTokenInfo.IsNullOrEmpty())
                {
                    foreach (var chain in item.ChainTokenInfo)
                    {
                        if (!_withdrawOptions.Value.PaymentAddresses?.ContainsKey(chain.ChainId) ?? false) continue;
                        var poolAddress = _withdrawOptions.Value.PaymentAddresses.GetValueOrDefault(chain.ChainId)?.GetValueOrDefault(item.Symbol);
                        if (poolAddress.IsNullOrEmpty()) continue;
                        tokenApplyList = AddTokenApplyList(tokenApplyList, item.Symbol, item.UserAddress, poolAddress, chain.ChainId);
                    }
                }
            }
            
            if (resultDto.Items.Count < PageSize) break;
        }

        if (tokenApplyList.IsNullOrEmpty()) return;
        _logger.LogInformation("TokenLiquidityTimerGrain query count: {count}", tokenApplyList.Count);
        try
        {
            var result = await _coBoProvider.GetAccountDetailAsync();
            if (result == null || result.Assets.IsNullOrEmpty()) return;
            foreach (var item in result.Assets)
            {
                if (!tokenApplyList.Exists(t => t.Coin == item.Coin)) continue;

                var coin = item.Coin.Split(CommonConstant.Underline);
                var symbol = coin.Length == 1 ? coin[0] : coin[1];
                var exchangeSymbolPair = string.Join(CommonConstant.Underline, symbol, CommonConstant.Symbol.USDT);
                var avgExchange = await GetExchchangeAsync(exchangeSymbolPair, item.Coin);
                if (avgExchange <= 0) continue;
                var amount = avgExchange * item.AbsBalance.SafeToDecimal();
                if (amount <= (!_tokenAccessOptions.Value.PoolConfig.ContainsKey(item.Coin)
                        ? _tokenAccessOptions.Value.DefaultPoolConfig.Liquidity.SafeToDecimal()
                        : _tokenAccessOptions.Value.PoolConfig[item.Coin].Liquidity.SafeToDecimal()))
                {
                    var monitorGrain = GrainFactory.GetGrain<IUserTokenAccessMonitorGrain>(item.Coin);
                    var tokenApply = tokenApplyList.FirstOrDefault(t => t.Coin == item.Coin);
                    tokenApply.Amount = amount.ToString();
                    await monitorGrain.DoLiquidityMonitor(tokenApply);
                }
            }

            var aelfTokenApplyList = tokenApplyList.Where(t => t.ChainId == ChainId.AELF
                                                               || t.ChainId == ChainId.tDVV ||
                                                               t.ChainId == ChainId.tDVW).ToList();
            if (aelfTokenApplyList.IsNullOrEmpty()) return;
            foreach (var item in aelfTokenApplyList)
            {
                var balance = await _contractProvider.CallTransactionAsync<GetBalanceOutput>(item.ChainId,
                    SystemContractName.TokenContract,
                    "GetBalance",
                    new GetBalanceInput
                    {
                        Owner = Address.FromBase58(item.PoolAddress),
                        Symbol = item.Symbol
                    });
                if (balance.Balance == 0) continue;
                var tokenGrain =
                    GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(item.Symbol, item.ChainId));
                var token = await tokenGrain.GetToken();
                var decimalPow = (decimal)Math.Pow(10, token.Decimals);
                var balanceDecimal = balance.Balance / decimalPow;

                var exchangeSymbolPair = string.Join(CommonConstant.Underline, item.Symbol, CommonConstant.Symbol.USDT);
                var avgExchange = await GetExchchangeAsync(exchangeSymbolPair, item.Coin);
                if (avgExchange <= 0) continue;
                var amount = avgExchange * balanceDecimal;
                if (amount <= (!_tokenAccessOptions.Value.PoolConfig.ContainsKey(item.Coin)
                        ? _tokenAccessOptions.Value.DefaultPoolConfig.Liquidity.SafeToDecimal()
                        : _tokenAccessOptions.Value.PoolConfig[item.Coin].Liquidity.SafeToDecimal()))
                {
                    var monitorGrain = GrainFactory.GetGrain<IUserTokenAccessMonitorGrain>(item.Coin);
                    var tokenApply = tokenApplyList.FirstOrDefault(t => t.Coin == item.Coin);
                    tokenApply.Amount = amount.ToString();
                    await monitorGrain.DoLiquidityMonitor(tokenApply);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TokenLiquidityTimerGrain query balance error.");
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }

    private List<TokenApplyDto> AddTokenApplyList(List<TokenApplyDto> tokenApplyList, string symbol, string address, 
        string poolAddress, string chainId)
    {
        if (tokenApplyList.Any(t => t.Symbol == symbol && t.Address == address && t.ChainId == chainId)) 
            return tokenApplyList;
                
        tokenApplyList.Add(new TokenApplyDto
        {
            Symbol = symbol,
            Address = address,
            PoolAddress = poolAddress,
            ChainId = chainId,
            Coin = string.Join(CommonConstant.Underline, chainId, symbol)
        });
        return tokenApplyList;
    }

    private async Task<decimal> GetExchchangeAsync(string exchangeSymbolPair, string coin)
    {
        var avgExchange = 0M;
        try
        {
            var exchangeGrain = GrainFactory.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
            var exchange = await exchangeGrain.GetAsync();
            AssertHelper.NotEmpty(exchange, "Exchange data not found {}", exchangeSymbolPair);
            avgExchange = exchange.Values
                .Where(ex => ex.Exchange > 0)
                .Average(ex => ex.Exchange);
            AssertHelper.IsTrue(avgExchange > 0, "Exchange amount error {}" + avgExchange);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exchange fail: {coin}", coin);
        }

        return avgExchange;
    }
}