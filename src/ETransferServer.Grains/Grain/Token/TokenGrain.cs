using AElf.Contracts.MultiToken;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.State.Token;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenGrain : IGrainWithStringKey
{
    Task<TokenDto> GetToken();

    public static string GenGrainId(string symbol, string chainId)
    {
        return TokenGrainId.Of(symbol, chainId).ToGrainId();
    }

    public static string GetNewId(Guid orderId)
    {
        return orderId.ToString().Replace(CommonConstant.Hyphen, CommonConstant.EmptyString);
    }
}

public class TokenGrain : Grain<TokenState>, ITokenGrain
{
    private readonly ILogger<TokenGrain> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IContractProvider _contractProvider;

    public TokenGrain(IObjectMapper objectMapper, IContractProvider contractProvider, ILogger<TokenGrain> logger)
    {
        _objectMapper = objectMapper;
        _contractProvider = contractProvider;
        _logger = logger;
    }

    public async Task<TokenDto> GetToken()
    {
        try
        {
            if (State.Symbol.NotNullOrEmpty())
                return _objectMapper.Map<TokenState, TokenDto>(State);

            var grainId = TokenGrainId.FromGrainId(this.GetPrimaryKeyString());
            if (grainId == null) return null;

            var tokenInfo = await _contractProvider.CallTransactionAsync<TokenInfo>(grainId.ChainId,
                SystemContractName.TokenContract, "GetTokenInfo", new GetTokenInfoInput { Symbol = grainId.Symbol });
            if (tokenInfo == null) return null;

            _objectMapper.Map(tokenInfo, State);
            await WriteStateAsync();
            _logger.LogInformation("Get token Info {Token}", JsonConvert.SerializeObject(State));

            return _objectMapper.Map<TokenState, TokenDto>(State);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get token failed {Pk}", this.GetPrimaryKeyString());
            throw;
        }
    }
}

public class TokenGrainId
{
    public string Symbol { get; set; }
    public string ChainId { get; set; }

    public TokenGrainId(string symbol, string chainId)
    {
        Symbol = symbol;
        ChainId = chainId;
    }

    public string ToGrainId()
    {
        return string.Join(CommonConstant.Hyphen, ChainId, Symbol);
    }


    public static TokenGrainId Of(string symbolId, string chainId)
    {
        return new TokenGrainId(symbolId, chainId);
    }

    public static TokenGrainId FromGrainId(string grainId)
    {
        var vals = grainId.Split(CommonConstant.Hyphen);
        if (vals.Length != 2 || vals[0].IsNullOrEmpty() || vals[1].IsNullOrEmpty())
        {
            return null;
        }

        return new TokenGrainId(vals[1], vals[0]);
    }
}