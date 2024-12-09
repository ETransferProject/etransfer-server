using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Options;
using ETransferServer.User;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using Nest;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.TokenAccess;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class TokenAccessAppService : ApplicationService, ITokenAccessAppService
{
    private readonly INESTRepository<TokenApplyOrderIndex, Guid> _tokenApplyOrderIndexRepository;
    private readonly INESTRepository<UserTokenAccessInfoIndex, Guid> _userTokenInfoIndexRepository;
    private readonly IUserAppService _userAppService;
    private readonly IOptionsSnapshot<NetworkOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenAccessAppService> _logger;
    private readonly IClusterClient _clusterClient;
    
    public TokenAccessAppService(INESTRepository<TokenApplyOrderIndex, Guid> tokenApplyOrderIndexRepository, 
        INESTRepository<UserTokenAccessInfoIndex, Guid> userTokenInfoIndexRepository,
        IUserAppService userAppService, 
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IOptionsSnapshot<TokenOptions> tokenOptions,
        IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions,
        IObjectMapper objectMapper,
        ILogger<TokenAccessAppService> logger,
        IClusterClient clusterClient
    )
    {
        _tokenApplyOrderIndexRepository = tokenApplyOrderIndexRepository;
        _userTokenInfoIndexRepository = userTokenInfoIndexRepository;
        _userAppService = userAppService;
        _networkInfoOptions = networkInfoOptions;
        _tokenOptions = tokenOptions;
        _tokenInfoOptions = tokenInfoOptions;
        _objectMapper = objectMapper;
        _logger = logger;
        _clusterClient = clusterClient;
    }
    
    public async Task<AvailableTokensDto> GetAvailableTokensAsync()
    {
        var result = new AvailableTokensDto();
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return result;
        var tokenOwnerGrain = _clusterClient.GetGrain<ITokenOwnerRecordGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        if (listDto == null || listDto.TokenOwnerList.IsNullOrEmpty()) return result;
        foreach (var token in listDto.TokenOwnerList)
        {
            var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(token.Symbol);
            result.TokenList.Add(new()
            {
                TokenName = token.TokenName,
                Symbol = token.Symbol,
                TokenImage = token.Icon,
                Holders = token.Holders,
                LiquidityInUsd = await tokenInvokeGrain.GetLiquidityInUsd()
            });
        }

        return result;
    }

    public async Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input)
    {
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        var tokenOwnerGrain = _clusterClient.GetGrain<ITokenOwnerRecordGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol), "Symbol invalid.");
        
        var userTokenAccessInfoGrain = _clusterClient.GetGrain<IUserTokenAccessInfoGrain>(
            string.Join(CommonConstant.Underline, input.Symbol, address));
        var dto = _objectMapper.Map<UserTokenAccessInfoInput, UserTokenAccessInfoDto>(input);
        dto.UserAddress = address;
        await userTokenAccessInfoGrain.AddOrUpdate(dto);
        return true;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenAccessAppService),
        MethodName = nameof(HandleAddOrUpdateUserTokenAccessInfoExceptionAsync))]
    public async Task AddOrUpdateUserTokenAccessInfoAsync(UserTokenAccessInfoDto dto)
    {
        await _userTokenInfoIndexRepository.AddOrUpdateAsync(ObjectMapper.Map<UserTokenAccessInfoDto, UserTokenAccessInfoIndex>(dto));
        Logger.LogInformation("Save token access info success, symbol:{symbol}", dto.Symbol);
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenAccessAppService),
        MethodName = nameof(HandleAddOrUpdateUserTokenApplyOrderExceptionAsync))]
    public async Task AddOrUpdateUserTokenApplyOrderAsync(TokenApplyOrderDto dto)
    {
        await _tokenApplyOrderIndexRepository.AddOrUpdateAsync(_objectMapper.Map<TokenApplyOrderDto, TokenApplyOrderIndex>(dto));
        Logger.LogInformation("Save token apply order success, symbol:{symbol}", dto.Symbol);
    }

    public async Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input)
    {
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        var tokenOwnerGrain = _clusterClient.GetGrain<ITokenOwnerRecordGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol), "Symbol invalid.");
        
        return _objectMapper.Map<UserTokenAccessInfoIndex, UserTokenAccessInfoDto>(await GetUserTokenAccessInfoIndexAsync(input.Symbol, address));
    }

    public async Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input)
    {
        var result = new CheckChainAccessStatusResultDto();
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        var tokenOwnerGrain = _clusterClient.GetGrain<ITokenOwnerRecordGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol), "Symbol invalid.");

        var networkList = _networkInfoOptions.Value.NetworkMap.OrderBy(m =>
                _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList().IndexOf(m.Key))
            .SelectMany(kvp => kvp.Value).Where(a =>
                a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).GroupBy(g => g.NetworkInfo.Network)
            .Select(s => s.First().NetworkInfo).ToList();
        
        result.ChainList.AddRange(networkList.Where(
            t => t.Network == ChainId.AELF || t.Network == ChainId.tDVV || t.Network == ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t.Network, ChainName = t.Name, Symbol = input.Symbol}));
        result.OtherChainList.AddRange(networkList.Where(
            t => t.Network != ChainId.AELF && t.Network != ChainId.tDVV && t.Network != ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t.Network, ChainName = t.Name, Symbol = input.Symbol }));

        
        var applyOrderList = await GetTokenApplyOrderIndexListAsync(address, input.Symbol);
        foreach (var item in result.ChainList)
        {
            var isCompleted = _tokenInfoOptions.Value.ContainsKey(item.Symbol) &&
                              _tokenInfoOptions.Value[item.Symbol].Transfer.Contains(item.ChainId);
            var tokenOwner = listDto.TokenOwnerList.LastOrDefault(t => t.Symbol == input.Symbol &&
                                                                        t.ChainIds.Contains(item.ChainId));
            item.TotalSupply = tokenOwner?.TotalSupply ?? 0;
            item.Decimals = tokenOwner?.Decimals ?? 0;
            item.TokenName = tokenOwner?.TokenName;
            item.Icon = tokenOwner?.Icon;
            item.Status = tokenOwner == null
                ? TokenApplyOrderStatus.Unissued.ToString()
                : isCompleted
                    ? TokenApplyOrderStatus.Complete.ToString()
                    : TokenApplyOrderStatus.Issued.ToString();
            item.Checked = isCompleted ||
                           applyOrderList.Exists(t => !t.ChainTokenInfo.IsNullOrEmpty() &&
                               t.ChainTokenInfo.Exists(c => c.ChainId == item.ChainId));
        }

        foreach (var item in result.OtherChainList)
        {
            var isCompleted = _tokenInfoOptions.Value.ContainsKey(item.Symbol) &&
                              _tokenInfoOptions.Value[item.Symbol].Transfer.Contains(item.ChainId);
            var tokenOwner = listDto.TokenOwnerList.FirstOrDefault(t => t.Symbol == input.Symbol &&
                                                                    t.ChainIds.Contains(item.ChainId));
            item.TotalSupply = tokenOwner?.TotalSupply ?? 0;
            item.Decimals = tokenOwner?.Decimals ?? 0;
            item.TokenName = tokenOwner?.TokenName;
            item.Icon = tokenOwner?.Icon;
            item.Status = tokenOwner == null
                ? TokenApplyOrderStatus.Unissued.ToString()
                : isCompleted
                    ? TokenApplyOrderStatus.Complete.ToString()
                    : TokenApplyOrderStatus.Issued.ToString();
            item.Checked = isCompleted ||
                           applyOrderList.Exists(t => t.OtherChainTokenInfo != null &&
                                                      t.OtherChainTokenInfo.ChainId == item.ChainId);
        }

        return result;
    }

    public async Task<AddChainResultDto> AddChainAsync(AddChainInput input)
    {
        AssertHelper.IsTrue(!input.ChainIds.IsNullOrEmpty() || !input.OtherChainIds.IsNullOrEmpty(), 
            "Param invalid.");
        var chainStatus = await CheckChainAccessStatusAsync(new CheckChainAccessStatusInput { Symbol = input.Symbol });
        AssertHelper.IsTrue(input.ChainIds.IsNullOrEmpty() || !input.ChainIds.Any(t => 
            !chainStatus.ChainList.Exists(c => c.ChainId == t)), "Param invalid.");
        AssertHelper.IsTrue(input.OtherChainIds.IsNullOrEmpty() || !input.OtherChainIds.Any(t => 
            !chainStatus.OtherChainList.Exists(c => c.ChainId == t)), "Param invalid.");
        
        var result = new AddChainResultDto();
        var address = await GetUserAddressAsync();
        if (!input.OtherChainIds.IsNullOrEmpty())
        {
            foreach (var item in input.OtherChainIds)
            {
                var chain = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == item);
                if (chain.Status != TokenApplyOrderStatus.Issued.ToString() &&
                    chain.Status != TokenApplyOrderStatus.Rejected.ToString()) continue;
                var orderId = GuidHelper.UniqGuid(input.Symbol, address, item);
                var tokenApplyOrderGrain = _clusterClient.GetGrain<IUserTokenApplyOrderGrain>(orderId);
                if (await tokenApplyOrderGrain.Get() != null ||
                    await GetTokenApplyOrderIndexAsync(orderId.ToString()) != null) continue;
                chain.Status = TokenApplyOrderStatus.Reviewing.ToString();
                var dto = new TokenApplyOrderDto
                {
                    Id = orderId,
                    Symbol = input.Symbol,
                    UserAddress = address,
                    Status = TokenApplyOrderStatus.Reviewing.ToString(),
                    ChainTokenInfo = chainStatus.ChainList.Where(t => input.ChainIds.Exists(c => c == t.ChainId))
                         .ToList().ConvertAll(t => _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(t)),
                    OtherChainTokenInfo = _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(chain)
                };
                dto.StatusChangedRecord ??= new Dictionary<string, string>();
                dto.StatusChangedRecord.AddOrReplace(TokenApplyOrderStatus.Reviewing.ToString(),
                    DateTime.UtcNow.ToUtcMilliSeconds().ToString());
                await tokenApplyOrderGrain.AddOrUpdate(dto);
                result.OtherChainList ??= new List<AddChainDto>();
                result.OtherChainList.Add(new()
                {
                    Id = orderId.ToString(),
                    ChainId = item
                });
            }
        }

        if (input.OtherChainIds.IsNullOrEmpty() && !input.ChainIds.IsNullOrEmpty() && input.ChainIds.Count == 1)
        {
            var chain = chainStatus.ChainList.FirstOrDefault(t => t.ChainId == input.ChainIds[0]);
            if (chain.Status != TokenApplyOrderStatus.Issued.ToString() &&
                chain.Status != TokenApplyOrderStatus.Rejected.ToString()) return result;
            var orderId = GuidHelper.UniqGuid(input.Symbol, address, input.ChainIds[0]);
            var tokenApplyOrderGrain = _clusterClient.GetGrain<IUserTokenApplyOrderGrain>(orderId);
            if (await tokenApplyOrderGrain.Get() != null ||
                await GetTokenApplyOrderIndexAsync(orderId.ToString()) != null) return result;
            chain.Status = TokenApplyOrderStatus.Reviewing.ToString();
            var dto = new TokenApplyOrderDto
            {
                Id = orderId,
                Symbol = input.Symbol,
                UserAddress = address,
                Status = TokenApplyOrderStatus.Reviewing.ToString(),
                ChainTokenInfo = new List<ChainTokenInfoDto> { _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(chain) }
            };
            dto.StatusChangedRecord ??= new Dictionary<string, string>();
            dto.StatusChangedRecord.AddOrReplace(TokenApplyOrderStatus.Reviewing.ToString(),
                DateTime.UtcNow.ToUtcMilliSeconds().ToString());
            await tokenApplyOrderGrain.AddOrUpdate(dto);
            result.ChainList ??= new List<AddChainDto>();
            result.ChainList.Add(new()
            {
                Id = orderId.ToString(),
                ChainId = input.ChainIds[0]
            });
        }
        return result;
    }

    public async Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input)
    {
        AssertHelper.IsTrue(!input.ChainId.IsNullOrEmpty() || !input.OtherChainId.IsNullOrEmpty(), 
            "Param invalid.");
        var chainStatus = await CheckChainAccessStatusAsync(new CheckChainAccessStatusInput { Symbol = input.Symbol });
        AssertHelper.IsTrue(input.ChainId.IsNullOrEmpty() || chainStatus.ChainList.Exists(
            c => c.ChainId == input.ChainId), "Param invalid.");
        AssertHelper.IsTrue(input.OtherChainId.IsNullOrEmpty() || chainStatus.OtherChainList.Exists(
            c => c.ChainId == input.OtherChainId), "Param invalid.");

        var key = string.Join(CommonConstant.Underline, input.Symbol, input.Address, input.OtherChainId);
        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(key);
        var dto = new UserTokenIssueDto
        {
            Id = GuidHelper.UniqGuid(input.Symbol, input.Address, input.OtherChainId),
            Address = await GetUserAddressAsync(),
            WalletAddress = input.Address,
            Symbol = input.Symbol,
            ChainId = input.ChainId,
            TokenName = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == input.OtherChainId).TokenName,
            TokenImage = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == input.OtherChainId).Icon,
            OtherChainId = input.OtherChainId,
            TotalSupply = input.Supply
        };
        return await tokenInvokeGrain.PrepareBinding(dto);
    }
    
    public async Task<bool> GetBindingIssueAsync(string id)
    {
        AssertHelper.IsTrue(!id.IsNullOrEmpty(), "Param invalid."); 
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        
        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(id);
        return await tokenInvokeGrain.Binding(id);
    }

    public async Task<PagedResultDto<TokenApplyOrderResultDto>> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input)
    {
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return new PagedResultDto<TokenApplyOrderResultDto>();
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (count, list) = await _tokenApplyOrderIndexRepository.GetSortListAsync(Filter,
            sortFunc: s => s.Ascending(a => a.UpdateTime), 
            skip: input.SkipCount, limit: input.MaxResultCount);
        return new PagedResultDto<TokenApplyOrderResultDto>
        {
            Items = await LoopCollectionItemsAsync(
                _objectMapper.Map<List<TokenApplyOrderIndex>, List<TokenApplyOrderResultDto>>(list), list),
            TotalCount = count
        };
    }

     public async Task<List<TokenApplyOrderResultDto>> GetTokenApplyOrderDetailAsync(GetTokenApplyOrderInput input)
    {
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return new List<TokenApplyOrderResultDto>();
        var list = await GetTokenApplyOrderIndexListAsync(address, input.Symbol, input.Id, input.ChainId);
        return await LoopCollectionItemsAsync(
            _objectMapper.Map<List<TokenApplyOrderIndex>, List<TokenApplyOrderResultDto>>(list), list);
    }
    
    private async Task<List<TokenApplyOrderResultDto>> LoopCollectionItemsAsync(List<TokenApplyOrderResultDto> itemList,
        List<TokenApplyOrderIndex> indexList)
    {
        foreach (var item in itemList)
        {
            item.StatusChangedRecord = null;
            var index = indexList.FirstOrDefault(i => i.Id == item.Id);
            if (item.Status == TokenApplyOrderStatus.Rejected.ToString())
            {
                item.RejectedTime = index != null && index.StatusChangedRecord != null &&
                                    index.StatusChangedRecord.ContainsKey(TokenApplyOrderStatus.Rejected.ToString())
                    ? index.StatusChangedRecord[TokenApplyOrderStatus.Rejected.ToString()].SafeToLong()
                    : 0L;
                item.RejectedReason = index != null && index.ExtensionInfo != null &&
                                      index.ExtensionInfo.ContainsKey(ExtensionKey.RejectedReason)
                    ? index.ExtensionInfo[ExtensionKey.RejectedReason]
                    : null;
            }
            else if (item.Status == TokenApplyOrderStatus.Failed.ToString())
            {
                item.FailedTime = index != null && index.StatusChangedRecord != null &&
                                  index.StatusChangedRecord.ContainsKey(TokenApplyOrderStatus.Failed.ToString())
                    ? index.StatusChangedRecord[TokenApplyOrderStatus.Failed.ToString()].SafeToLong()
                    : 0L;
                item.FailedReason = index != null && index.ExtensionInfo != null &&
                                    index.ExtensionInfo.ContainsKey(ExtensionKey.FailedReason)
                    ? index.ExtensionInfo[ExtensionKey.FailedReason]
                    : null;
            }
        }

        return itemList;
    }

    private async Task<string> GetUserAddressAsync()
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue) return null;
        var userDto = await _userAppService.GetUserByIdAsync(userId.Value.ToString());
        return userDto?.AddressInfos?.FirstOrDefault()?.Address;
    }
    
    private async Task<UserTokenAccessInfoIndex> GetUserTokenAccessInfoIndexAsync(string symbol, string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserTokenAccessInfoIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Symbol).Value(symbol)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        QueryContainer Filter(QueryContainerDescriptor<UserTokenAccessInfoIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _userTokenInfoIndexRepository.GetAsync(Filter);
    }
    
    private async Task<TokenApplyOrderIndex> GetTokenApplyOrderIndexAsync(string id)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Id).Value(id)));
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _tokenApplyOrderIndexRepository.GetAsync(Filter);
    }
    
    private async Task<List<TokenApplyOrderIndex>> GetTokenApplyOrderIndexListAsync(string address, string symbol, 
        string id = null, string chainId = null)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Symbol).Value(symbol)));
        if (!id.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i => i.Field(f => f.Id).Value(id)));
        }
        if (!chainId.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Match(k =>
                    k.Field("chainTokenInfo.chainId").Query(chainId)),
                s => s.Term(k =>
                    k.Field(f => f.OtherChainTokenInfo.ChainId).Value(chainId)))));
        }
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var result = await _tokenApplyOrderIndexRepository.GetListAsync(Filter);
        return result.Item2;
    }
}