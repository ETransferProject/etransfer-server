using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.Samples.HttpClient;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenOwnerTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenOwnerTimerGrain : Grain<TokenOwnerState>, ITokenOwnerTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    private const int PageSize = 50;
    private ApiInfo _tokenListUri => new(HttpMethod.Get, _tokenAccessOptions.Value.ScanTokenListUri);
    
    public TokenOwnerTimerGrain(IHttpProvider httpProvider, 
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        IObjectMapper objectMapper,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _httpProvider = httpProvider;
        _timerOptions = timerOptions;
        _tokenAccessOptions = tokenAccessOptions;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenOwnerTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenOwnerTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenOwnerTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenOwnerTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenOwnerTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;
        var skipCount = 0;

        while (true)
        {
            var resultDto = new ScanTokenListResultDto();
            var tokenDic = new Dictionary<string, TokenOwnerListDto>();
            try
            {
                var tokenParams = new Dictionary<string, string>();
                tokenParams["skipCount"] = skipCount.ToString();
                tokenParams["maxResultCount"] = PageSize.ToString();
                tokenParams["sort"] = "Desc";
                tokenParams["orderBy"] = "HolderCount";
                resultDto = await _httpProvider.InvokeAsync<ScanTokenListResultDto>(_tokenAccessOptions.Value.ScanBaseUrl, _tokenListUri, param: tokenParams);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get aelfscan tokens error.");
            }
        
            if (resultDto.Code != "20000" && resultDto.Data.List.Count < 0)
            {
                break;
            }

            skipCount += resultDto.Data.List.Count;
            foreach (var item in resultDto.Data.List)
            {
                var tokenGrain =
                    GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(item.Token.Symbol, 
                        item.ChainIds.IsNullOrEmpty() ? ChainId.AELF : item.ChainIds[0]));
                var tokenInfo = await tokenGrain.GetToken();
                if (tokenInfo == null || tokenInfo.Owner.IsNullOrEmpty()) continue;

                var tokenOwnerList = tokenDic.GetOrAdd(tokenInfo.Owner, _ => new TokenOwnerListDto());
                if (tokenOwnerList.TokenOwnerList.Any(t => t.Symbol == item.Token.Symbol)) continue;
                tokenOwnerList.TokenOwnerList.Add(new TokenOwnerDto {
                    TokenName = item.Token.Name,
                    Symbol = item.Token.Symbol,
                    Decimals = item.Token.Decimals.SafeToInt(),
                    Icon = item.Token.ImageUrl,
                    Owner = tokenInfo.Owner,
                    ChainIds = item.ChainIds,
                    TotalSupply = item.TotalSupply,
                    Holders = item.Holders,
                    Status = TokenApplyOrderStatus.Issued.ToString()
                });
            }
            foreach (var kv in tokenDic)
            {
                var tokenOwnerRecordGrain = GrainFactory.GetGrain<ITokenOwnerRecordGrain>(kv.Key);
                var listDto = await tokenOwnerRecordGrain.Get();
                if (listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty())
                {
                    var toAdd = kv.Value.TokenOwnerList.Except(listDto.TokenOwnerList);
                    if (!toAdd.Any()) continue;
                    await tokenOwnerRecordGrain.AddOrUpdate(new TokenOwnerListDto
                    {
                        TokenOwnerList = toAdd.ToList()
                    });
                }
                else
                {
                    await tokenOwnerRecordGrain.AddOrUpdate(kv.Value);
                }
            }
        
            if (resultDto.Data.List.Count < PageSize) break;
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}

public class ScanTokenListResultDto
{
    public string Code { get; set; }
    public ScanDataDto Data { get; set; }
}

public class ScanDataDto {
    public int Total { get; set; }
    public List<ScanTokenItem> List { get; set; }
}

public class ScanTokenItem
{
    public long TotalSupply { get; set; }
    public int Holders { get; set; }
    public TokenItem Token { get; set; }
    public List<string> ChainIds { get; set; }
}

public class TokenItem
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public string ImageUrl { get; set; }
    public string Decimals { get; set; }
}