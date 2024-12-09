using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.Samples.HttpClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenInvokeGrain : IGrainWithStringKey
{
    Task<string> GetLiquidityInUsd();
    Task<string> PrepareBinding(UserTokenIssueDto dto);
    Task<bool> Binding(string bindingId);
}

public class TokenInvokeGrain : Grain<TokenInvokeState>, ITokenInvokeGrain
{
    private readonly ILogger<TokenGrain> _logger;
    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private ApiInfo _tokenLiquidityUri => new(HttpMethod.Get, _tokenAccessOptions.Value.AwakenGetTokenLiquidityUri);
    private ApiInfo _prepareBindingUri => new(HttpMethod.Get, _tokenAccessOptions.Value.SymbolMarketPrepareBindingUri);
    private ApiInfo _bindingUri => new(HttpMethod.Get, _tokenAccessOptions.Value.SymbolMarketBindingUri);

    public TokenInvokeGrain(ILogger<TokenGrain> logger, 
        IHttpProvider httpProvider,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _tokenAccessOptions = tokenAccessOptions;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain), 
        MethodName = nameof(HandleGetLiquidityExceptionAsync))]
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain), 
        MethodName = nameof(HandlePrepareBindingExceptionAsync))]
    public async Task<string> PrepareBinding(UserTokenIssueDto dto)
    {
        var userTokenIssueGrain = GrainFactory.GetGrain<IUserTokenIssueGrain>(dto.Id);
        var res = await userTokenIssueGrain.Get();
        if (res != null && !res.BindingId.IsNullOrEmpty()) return res.BindingId;

        var url =
            $"{_tokenAccessOptions.Value.SymbolMarketBaseUrl}{_tokenAccessOptions.Value.SymbolMarketPrepareBindingUri}";
        var resultDto = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(HttpMethod.Post, url,
            body: JsonConvert.SerializeObject(new PrepareBindingInput
            {
                Address = dto.WalletAddress,
                AelfToken = dto.Symbol,
                AelfChain = dto.ChainId,
                ThirdTokens = new ThirdToken
                {
                    TokenName = dto.TokenName,
                    Symbol = dto.Symbol,
                    TokenImage = dto.TokenImage,
                    TotalSupply = dto.TotalSupply,
                    ThirdChain = dto.OtherChainId
                }
            }, HttpProvider.DefaultJsonSettings));
        if (resultDto.Code == "20000")
        {
            dto.BindingId = resultDto.Value;
            await userTokenIssueGrain.AddOrUpdate(dto);
            return dto.BindingId;
        }

        return string.Empty;
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain), 
        MethodName = nameof(HandleBindingExceptionAsync))]
    public async Task<bool> Binding(string bindingId)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State != null && !State.BindingStatus.IsNullOrEmpty())
        {
            return State.BindingStatus == TokenApplyOrderStatus.Issued.ToString();
        }

        var url =
            $"{_tokenAccessOptions.Value.SymbolMarketBaseUrl}{_tokenAccessOptions.Value.SymbolMarketBindingUri}";
        var resultDto = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(HttpMethod.Post, url,
            body: JsonConvert.SerializeObject(new BindingInput
            {
                BindingId = bindingId
            }, HttpProvider.DefaultJsonSettings));
        if (resultDto.Code == "20000" && CommonConstant.SuccessStatus.Equals(resultDto.Value, StringComparison.CurrentCultureIgnoreCase))
        {
            State.LastModifyTime = now;
            State.BindingStatus = TokenApplyOrderStatus.Issued.ToString();
            await WriteStateAsync();
            return true;
        }
        
        return false;
    }
    
    public async Task<FlowBehavior> HandleGetLiquidityExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get token liquidity failed. {symbol}", this.GetPrimaryKeyString());
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = State.LiquidityInUsd
        };
    }
    
    public async Task<FlowBehavior> HandlePrepareBindingExceptionAsync(Exception ex, UserTokenIssueDto dto)
    {
        _logger.LogError(ex, "Post prepare binding failed. {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = string.Empty
        };
    }
    
    public async Task<FlowBehavior> HandleBindingExceptionAsync(Exception ex, string bindingId)
    {
        _logger.LogError(ex, "Post binding failed. {id}", bindingId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}

public class PrepareBindingInput
{
    public string Address { get; set; }
    public string AelfToken { get; set; }
    public string AelfChain { get; set; }
    public ThirdToken ThirdTokens { get; set; }
}

public class ThirdToken
{
    public string TokenName { get; set; }
    public string Symbol { get; set; }
    public string TokenImage { get; set; }          
    public string TotalSupply { get; set; }
    public string ThirdChain { get; set; }
}

public class BindingInput
{
    public string BindingId { get; set; }
}