using AElf;
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
    Task<UserTokenBindingDto> PrepareBinding(UserTokenIssueDto dto);
    Task<bool> Binding(UserTokenBindingDto dto);
    Task<bool> AddOrUpdateTokenIssue(Guid issueId);
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
    public async Task<UserTokenBindingDto> PrepareBinding(UserTokenIssueDto dto)
    {
        var userTokenIssueGrain = GrainFactory.GetGrain<IUserTokenIssueGrain>(dto.Id);
        var res = await userTokenIssueGrain.Get();
        if (res != null && !res.BindingId.IsNullOrEmpty() && !res.ThirdTokenId.IsNullOrEmpty())
            return new UserTokenBindingDto { BindingId = res.BindingId, ThirdTokenId = res.ThirdTokenId };

        var url =
            $"{_tokenAccessOptions.Value.SymbolMarketBaseUrl}{_tokenAccessOptions.Value.SymbolMarketPrepareBindingUri}";
        var resultDto = await _httpProvider.InvokeAsync<CommonResponseDto<UserTokenBindingDto>>(HttpMethod.Post, url,
            body: JsonConvert.SerializeObject(new PrepareBindingInput
            {
                Address = dto.Address,
                AelfToken = dto.Symbol,
                AelfChain = dto.ChainId,
                ThirdTokens = new ThirdTokenDto
                {
                    TokenName = dto.TokenName,
                    Symbol = dto.Symbol,
                    TokenImage = dto.TokenImage,
                    TotalSupply = dto.TotalSupply,
                    ThirdChain = dto.OtherChainId,
                    Owner = dto.WalletAddress,
                    ContractAddress = dto.ContractAddress
                },
                Signature = BuildRequestHash(string.Concat(dto.Address, dto.Symbol, dto.ChainId, dto.TokenName, 
                    dto.Symbol, dto.TokenImage, dto.TotalSupply, dto.WalletAddress, dto.OtherChainId, 
                    dto.ContractAddress))
            }, HttpProvider.DefaultJsonSettings));
        if (resultDto.Code == "20000")
        {
            dto.BindingId = resultDto.Value?.BindingId;
            dto.ThirdTokenId = resultDto.Value?.ThirdTokenId;
            await userTokenIssueGrain.AddOrUpdate(dto);
            var tokenInvokeGrain = GrainFactory.GetGrain<ITokenInvokeGrain>(
                string.Join(CommonConstant.Underline, dto.BindingId, dto.ThirdTokenId));
            await tokenInvokeGrain.AddOrUpdateTokenIssue(dto.Id);
            return new UserTokenBindingDto { BindingId = dto.BindingId, ThirdTokenId = dto.ThirdTokenId };
        }

        return new UserTokenBindingDto();
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain), 
        MethodName = nameof(HandleBindingExceptionAsync))]
    public async Task<bool> Binding(UserTokenBindingDto dto)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State != null && State.UserTokenIssueId != Guid.Empty)
        {
            var userTokenIssueGrain = GrainFactory.GetGrain<IUserTokenIssueGrain>(State.UserTokenIssueId);
            var res = await userTokenIssueGrain.Get();
            if (res != null && res.Status == TokenApplyOrderStatus.Issued.ToString()) return true;
        }

        var url =
            $"{_tokenAccessOptions.Value.SymbolMarketBaseUrl}{_tokenAccessOptions.Value.SymbolMarketBindingUri}";
        var resultDto = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(HttpMethod.Post, url,
            body: JsonConvert.SerializeObject(new BindingInput
            {
                BindingId = dto.BindingId,
                ThirdTokenId = dto.ThirdTokenId,
                Signature = BuildRequestHash(string.Concat(dto.BindingId, dto.ThirdTokenId))
            }, HttpProvider.DefaultJsonSettings));
        if (resultDto.Code == "20000" && State != null && State.UserTokenIssueId != Guid.Empty)
        {
            var userTokenIssueGrain = GrainFactory.GetGrain<IUserTokenIssueGrain>(State.UserTokenIssueId);
            var res = await userTokenIssueGrain.Get();
            res.BindingId = dto.BindingId;
            res.ThirdTokenId = dto.ThirdTokenId;
            res.Status = TokenApplyOrderStatus.Issued.ToString();
            await userTokenIssueGrain.AddOrUpdate(res);
            return true;
        }
        
        return false;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenInvokeGrain),
        MethodName = nameof(HandleAddOrUpdateBindingExceptionAsync))]
    public async Task<bool> AddOrUpdateTokenIssue(Guid issueId)
    {
        State.LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds();
        State.UserTokenIssueId = issueId;
        await WriteStateAsync();
        return true;
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
    
    public async Task<FlowBehavior> HandleBindingExceptionAsync(Exception ex, UserTokenBindingDto dto)
    {
        _logger.LogError(ex, "Post binding failed. {bindingId},{thirdTokenId}", dto.BindingId, dto.ThirdTokenId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }

    public async Task<FlowBehavior> HandleAddOrUpdateBindingExceptionAsync(Exception ex, Guid issueId)
    {
        _logger.LogError(ex, "Save binding failed. {primaryKey},{issueId}", this.GetPrimaryKeyString(), issueId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    private string BuildRequestHash(string request)
    {
        var hashVerifyKey = _tokenAccessOptions.Value.HashVerifyKey;
        var requestHash = HashHelper.ComputeFrom(string.Concat(request, hashVerifyKey));
        return requestHash.ToHex();
    }
}

public class PrepareBindingInput
{
    public string Address { get; set; }
    public string AelfToken { get; set; }
    public string AelfChain { get; set; }
    public ThirdTokenDto ThirdTokens { get; set; }
    public string Signature { get; set; }
}

public class ThirdTokenDto
{
    public string TokenName { get; set; }
    public string Symbol { get; set; }
    public string TokenImage { get; set; }          
    public string TotalSupply { get; set; }
    public string ThirdChain { get; set; }
    public string Owner { get; set; }
    public string ContractAddress { get; set; }
}

public class BindingInput
{
    public string BindingId { get; set; }
    public string ThirdTokenId { get; set; }
    public string Signature { get; set; }
}