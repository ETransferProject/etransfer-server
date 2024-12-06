using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Common.HttpClient;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.Samples.HttpClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenInvokeGrain : IGrainWithStringKey
{
    Task<string> GetLiquidityInUsd();
}

public class TokenInvokeGrain : Grain<TokenInvokeState>, ITokenInvokeGrain
{
    private readonly ILogger<TokenGrain> _logger;
    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private ApiInfo _tokenLiquidityUri => new(HttpMethod.Get, _tokenAccessOptions.Value.AwakenGetTokenLiquidityUri);

    public TokenInvokeGrain(ILogger<TokenGrain> logger, 
        IHttpProvider httpProvider,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _tokenAccessOptions = tokenAccessOptions;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<string> GetLiquidityInUsd()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.LastModifyTime > 0 && State.ExpireTime > now)
        {
            return State.LiquidityInUsd;
        }

        var tokenParams = new Dictionary<string, string>();
        tokenParams["symbol"] = this.GetPrimaryKeyString();
        var resultDto = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(_tokenAccessOptions.Value.AwakenBaseUrl, _tokenLiquidityUri, param: tokenParams);
        if (resultDto.Code == "20000")
        {
            State.LiquidityInUsd = resultDto.Value;
        }
        
        State.LastModifyTime = now;
        State.ExpireTime = now + _tokenAccessOptions.Value.DataExpireSeconds * 1000;
        await WriteStateAsync();
        return State.LiquidityInUsd;
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get token liquidity failed. {symbl}", this.GetPrimaryKeyString());
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = State.LiquidityInUsd
        };
    }
}