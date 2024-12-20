using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.TokenAccess;
using NBitcoin;
using Volo.Abp.Application.Dtos;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenIntegrateTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenIntegrateTimerGrain : Grain<TokenIntegrateState>, ITokenIntegrateTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly ITokenAccessAppService _tokenAccessAppService;
    private readonly IObjectMapper _objectMapper;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    private const int PageSize = 1000;
    
    public TokenIntegrateTimerGrain(ITokenAccessAppService tokenAccessAppService,
        IObjectMapper objectMapper,
        IOptionsSnapshot<TimerOptions> timerOptions,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _tokenAccessAppService = tokenAccessAppService;
        _objectMapper = objectMapper;
        _timerOptions = timerOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenIntegrateTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenIntegrateTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenIntegrateTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenIntegrateTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenIntegrateTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;
        var skipCount = 0;

        while (true)
        {
            var resultDto = new PagedResultDto<TokenApplyOrderResultDto>();
            try
            {
                resultDto = await _tokenAccessAppService.GetTokenApplyListAsync(
                    new GetTokenApplyOrderListInput
                    {
                        Status = TokenApplyOrderStatus.PoolInitialized.ToString(),
                        SkipCount = skipCount,
                        MaxResultCount = PageSize
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get poolInitialized token apply order list error.");
            }
        
            if (resultDto == null || resultDto.Items == null || resultDto.Items.Count == 0)
            {
                break;
            }
        
            _logger.LogDebug("TokenIntegrateTimerGrain call, {skipCount}", skipCount);
            skipCount += resultDto.Items.Count;
            foreach (var item in resultDto.Items)
            {
                var chainId = item.OtherChainTokenInfo?.ChainId ?? (!item.ChainTokenInfo.IsNullOrEmpty()
                    ? item.ChainTokenInfo[0].ChainId
                    : null);
                if (chainId.IsNullOrEmpty()) continue;
                var contractAddress = item.OtherChainTokenInfo?.ContractAddress ?? (!item.ChainTokenInfo.IsNullOrEmpty()
                    ? item.ChainTokenInfo[0].ContractAddress
                    : null);
                var orderId = GuidHelper.UniqGuid(item.Symbol, item.UserAddress, chainId);
                var tokenApplyOrderGrain = GrainFactory.GetGrain<IUserTokenApplyOrderGrain>(orderId);
                var dto = await tokenApplyOrderGrain.Get() ?? 
                          _objectMapper.Map<TokenApplyOrderResultDto, TokenApplyOrderDto>(item);
                dto.Status = TokenApplyOrderStatus.Integrating.ToString();
                if (dto.OtherChainTokenInfo != null) dto.OtherChainTokenInfo.Status = dto.Status;
                if (!dto.ChainTokenInfo.IsNullOrEmpty()) dto.ChainTokenInfo.ForEach(t => t.Status = dto.Status);
                dto.StatusChangedRecord ??= new Dictionary<string, string>();
                dto.StatusChangedRecord.AddOrReplace(TokenApplyOrderStatus.Integrating.ToString(),
                    DateTime.UtcNow.ToUtcMilliSeconds().ToString());
                await tokenApplyOrderGrain.AddOrUpdate(dto);
                var monitorGrain = GrainFactory.GetGrain<IUserTokenAccessMonitorGrain>(item.Id.ToString());
                await monitorGrain.DoTokenIntegrateMonitor(new TokenApplyDto
                {
                    Symbol = item.Symbol,
                    ContractAddress = contractAddress,
                    ChainId = chainId
                });
            }
            
            if (resultDto.Items.Count < PageSize) break;
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}