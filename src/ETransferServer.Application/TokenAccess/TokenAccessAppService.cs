using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Options;
using ETransferServer.TokenAccess.Provider;
using ETransferServer.User;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.TokenAccess;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TokenAccessAppService : ApplicationService, ITokenAccessAppService
{
    private readonly ISymbolMarketProvider _symbolMarketProvider;
    private readonly ILiquidityDataProvider _liquidityDataProvider;
    private readonly IScanProvider _scanProvider;
    private readonly INESTRepository<TokenApplyOrderIndex, Guid> _tokenApplyOrderIndexRepository;
    private readonly INESTRepository<UserTokenAccessInfoIndex, Guid> _userTokenInfoIndexRepository;
    private readonly IUserAppService _userAppService;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly IOptionsSnapshot<NetworkOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenAccessAppService> _logger;
    private readonly IClusterClient _clusterClient;
    
    public TokenAccessAppService(ISymbolMarketProvider symbolMarketProvider,
        ILiquidityDataProvider liquidityDataProvider,
        IScanProvider scanProvider,
        INESTRepository<TokenApplyOrderIndex, Guid> tokenApplyOrderIndexRepository, 
        INESTRepository<UserTokenAccessInfoIndex, Guid> userTokenInfoIndexRepository,
        IUserAppService userAppService, 
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IOptionsSnapshot<TokenOptions> tokenOptions,
        IObjectMapper objectMapper,
        ILogger<TokenAccessAppService> logger,
        IClusterClient clusterClient
    )
    {
        _symbolMarketProvider = symbolMarketProvider;
        _liquidityDataProvider = liquidityDataProvider;
        _scanProvider = scanProvider;
        _tokenApplyOrderIndexRepository = tokenApplyOrderIndexRepository;
        _userTokenInfoIndexRepository = userTokenInfoIndexRepository;
        _userAppService = userAppService;
        _tokenAccessOptions = tokenAccessOptions;
        _networkInfoOptions = networkInfoOptions;
        _tokenOptions = tokenOptions;
        _objectMapper = objectMapper;
        _logger = logger;
        _clusterClient = clusterClient;
    }
    
    public async Task<AvailableTokensDto> GetAvailableTokensAsync()
    {
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return new AvailableTokensDto();
        var tokenList = await _scanProvider.GetOwnTokensAsync(address);
        foreach (var token in tokenList)
        {
            token.LiquidityInUsd = await _liquidityDataProvider.GetTokenTvlAsync(token.Symbol);
        }

        return new AvailableTokensDto
        {
            TokenList = tokenList
        };
    }

    public async Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input)
    {
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return false;
        var tokenGrain =
            _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(input.Symbol, ChainId.AELF));
        var tokenInfo = await tokenGrain.GetToken();
        if (tokenInfo.Owner != address)
        {
            _logger.LogInformation("CommitTokenAccessInfoAsync no permission.");
            return false;
        }

        var userTokenAccessInfoIndex = await GetUserTokenAccessInfoIndexAsync(input.Symbol);
        var index = _objectMapper.Map<UserTokenAccessInfoInput, UserTokenAccessInfoIndex>(input);
        index.Id = userTokenAccessInfoIndex?.Id ?? Guid.NewGuid();
        index.UserAddress = address;
        _userTokenInfoIndexRepository.AddOrUpdateAsync(index);
        return true;
    }

    public async Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input)
    {
        return _objectMapper.Map<UserTokenAccessInfoIndex, UserTokenAccessInfoDto>(await GetUserTokenAccessInfoIndexAsync(input.Symbol));
    }

    public async Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input)
    {
        var result = new CheckChainAccessStatusResultDto();

        var networkList = _networkInfoOptions.Value.NetworkMap.OrderBy(m =>
                _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList().IndexOf(m.Key))
            .SelectMany(kvp => kvp.Value).Where(a =>
                a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).GroupBy(g => g.NetworkInfo.Network)
            .Select(s => s.First().NetworkInfo.Network).ToList();
        
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(input.Symbol),
            ErrorResult.SymbolInvalidCode, null, input.Symbol);
        result.ChainList.AddRange(networkList.Where(
            t => t == ChainId.AELF || t == ChainId.tDVV || t == ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t, Status = TokenApplyOrderStatus.Complete.ToString()}));
        result.OtherChainList.AddRange(networkList.Where(
            t => t != ChainId.AELF && t != ChainId.tDVV && t != ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t }));

        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return result;
        
        var applyOrderList = await GetTokenApplyOrderIndexListAsync(address, input.Symbol);
        foreach (var applyOrderIndex in applyOrderList)
        {
            var otherChain = result.OtherChainList.FirstOrDefault(t => t.ChainId == applyOrderIndex.OtherChainTokenInfo.ChainId);
            if (otherChain != null) otherChain.Status = applyOrderIndex.Status;
        }
        
        return result;
    }

    public async Task<SelectChainDto> SelectChainAsync(SelectChainInput input)
    {
        throw new NotImplementedException();
    }

    public async Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input)
    {
        throw new NotImplementedException();
    }
    
    public async Task<bool> GetBindingIssueAsync(string id)
    {
        throw new NotImplementedException();
    }

    public async Task<TokenApplyOrderListDto> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input)
    {
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return new TokenApplyOrderListDto();
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (count, list) = await _tokenApplyOrderIndexRepository.GetSortListAsync(Filter,
            sortFunc: s => s.Ascending(a => a.UpdateTime), 
            skip: input.SkipCount, limit: input.MaxResultCount);
        return new TokenApplyOrderListDto
        {
            Items = _objectMapper.Map<List<TokenApplyOrderIndex>, List<TokenApplyOrderDto>>(list),
            TotalCount = count
        };
    }

    public async Task<TokenApplyOrderDto> GetTokenApplyOrderAsync(string id)
    {
        if (!Guid.TryParse(id, out _)) return new TokenApplyOrderDto();
        var tokenApplyOrder = await _tokenApplyOrderIndexRepository.GetAsync(Guid.Parse(id));
        return _objectMapper.Map<TokenApplyOrderIndex, TokenApplyOrderDto>(tokenApplyOrder);
    }

    private async Task<string> GetUserAddressAsync()
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue) return null;
        var userDto = await _userAppService.GetUserByIdAsync(userId.Value.ToString());
        return userDto?.AddressInfos?.FirstOrDefault()?.Address;
    }
    
    private async Task<UserTokenAccessInfoIndex> GetUserTokenAccessInfoIndexAsync(string symbol)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserTokenAccessInfoIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Symbol).Value(symbol)));
        QueryContainer Filter(QueryContainerDescriptor<UserTokenAccessInfoIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _userTokenInfoIndexRepository.GetAsync(Filter);
    }
    
    private async Task<List<TokenApplyOrderIndex>> GetTokenApplyOrderIndexListAsync(string address, string symbol = null)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        if (String.IsNullOrWhiteSpace(symbol))
        {
            mustQuery.Add(q => q.Term(i => i.Field(f => f.Symbol).Value(symbol)));
        }
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var result = await _tokenApplyOrderIndexRepository.GetListAsync(Filter);
        return result.Item2;
    }
}