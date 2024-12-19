using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using AElf.Types;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
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
    private readonly IContractProvider _contractProvider;
    private readonly IOptionsSnapshot<NetworkOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenAccessAppService> _logger;
    private readonly IClusterClient _clusterClient;
    
    public TokenAccessAppService(INESTRepository<TokenApplyOrderIndex, Guid> tokenApplyOrderIndexRepository, 
        INESTRepository<UserTokenAccessInfoIndex, Guid> userTokenInfoIndexRepository,
        IUserAppService userAppService, 
        IContractProvider contractProvider,
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IOptionsSnapshot<TokenOptions> tokenOptions,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions,
        IObjectMapper objectMapper,
        ILogger<TokenAccessAppService> logger,
        IClusterClient clusterClient
    )
    {
        _tokenApplyOrderIndexRepository = tokenApplyOrderIndexRepository;
        _userTokenInfoIndexRepository = userTokenInfoIndexRepository;
        _userAppService = userAppService;
        _contractProvider = contractProvider;
        _networkInfoOptions = networkInfoOptions;
        _tokenOptions = tokenOptions;
        _tokenAccessOptions = tokenAccessOptions;
        _tokenInfoOptions = tokenInfoOptions;
        _objectMapper = objectMapper;
        _logger = logger;
        _clusterClient = clusterClient;
    }

    public async Task<TokenConfigDto> GetTokenConfigAsync(GetTokenConfigInput input)
    {
        return new TokenConfigDto
        {
            LiquidityInUsd = !_tokenAccessOptions.Value.TokenConfig.ContainsKey(input.Symbol)
                ? _tokenAccessOptions.Value.DefaultConfig.Liquidity
                : _tokenAccessOptions.Value.TokenConfig[input.Symbol].Liquidity,
            Holders = !_tokenAccessOptions.Value.TokenConfig.ContainsKey(input.Symbol)
                ? _tokenAccessOptions.Value.DefaultConfig.Holders
                : _tokenAccessOptions.Value.TokenConfig[input.Symbol].Holders
        };
    }

    public async Task<AvailableTokensDto> GetAvailableTokensAsync()
    {
        var result = new AvailableTokensDto();
        var address = await GetUserAddressAsync();
        if (address.IsNullOrEmpty()) return result;
        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(address);
        var listDto = await tokenInvokeGrain.GetUserTokenOwnerList();
        if (listDto == null || listDto.TokenOwnerList.IsNullOrEmpty()) return result;
        foreach (var token in listDto.TokenOwnerList)
        {
            result.TokenList.Add(new()
            {
                TokenName = token.TokenName,
                Symbol = token.Symbol,
                TokenImage = token.Icon,
                Holders = token.Holders,
                LiquidityInUsd = token.LiquidityInUsd
            });
        }

        return result;
    }

    public async Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input)
    {
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        AssertHelper.IsTrue(input.Email.Contains(CommonConstant.At), "Please enter a valid email address"); 
        var tokenOwnerGrain = _clusterClient.GetGrain<IUserTokenOwnerGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol) &&
            CheckLiquidityAndHolderAvailable(listDto.TokenOwnerList, input.Symbol), "Symbol invalid.");
        
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
        var tokenOwnerGrain = _clusterClient.GetGrain<IUserTokenOwnerGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol) &&
            CheckLiquidityAndHolderAvailable(listDto.TokenOwnerList, input.Symbol), "Symbol invalid.");
        
        return _objectMapper.Map<UserTokenAccessInfoIndex, UserTokenAccessInfoDto>(await GetUserTokenAccessInfoIndexAsync(input.Symbol, address));
    }

    public async Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input)
    {
        var result = new CheckChainAccessStatusResultDto();
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        var tokenOwnerGrain = _clusterClient.GetGrain<IUserTokenOwnerGrain>(address);
        var listDto = await tokenOwnerGrain.Get();
        AssertHelper.IsTrue(listDto != null && !listDto.TokenOwnerList.IsNullOrEmpty() &&
            listDto.TokenOwnerList.Exists(t => t.Symbol == input.Symbol) &&
            CheckLiquidityAndHolderAvailable(listDto.TokenOwnerList, input.Symbol), "Symbol invalid.");

        var networkList = _networkInfoOptions.Value.NetworkMap.OrderBy(m =>
                _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList().IndexOf(m.Key))
            .SelectMany(kvp => kvp.Value).Where(a => a.NetworkInfo.IsTokenAccessRange &&
                a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).GroupBy(g => g.NetworkInfo.Network)
            .Select(s => s.First().NetworkInfo).ToList();
        
        result.ChainList.AddRange(networkList.Where(
            t => t.Network == ChainId.AELF || t.Network == ChainId.tDVV || t.Network == ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t.Network, ChainName = t.Name, Symbol = input.Symbol }));
        result.OtherChainList.AddRange(networkList.Where(
            t => t.Network != ChainId.AELF && t.Network != ChainId.tDVV && t.Network != ChainId.tDVW).Select(
            t => new ChainAccessInfo { ChainId = t.Network, ChainName = t.Name, Symbol = input.Symbol }));

        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(
            string.Join(CommonConstant.Underline, input.Symbol, address));
        await tokenInvokeGrain.GetThirdTokenList(address, input.Symbol);
        var applyOrderList = await GetTokenApplyOrderIndexListAsync(address, input.Symbol);
        foreach (var item in result.ChainList)
        {
            var isCompleted = _tokenInfoOptions.Value.ContainsKey(item.Symbol) &&
                              _tokenInfoOptions.Value[item.Symbol].Transfer.Contains(item.ChainId);
            var tokenOwner = listDto.TokenOwnerList.FirstOrDefault(t => t.Symbol == input.Symbol &&
                                                                        t.ChainIds.Contains(item.ChainId));
            var applyStatus = applyOrderList.FirstOrDefault(t => !t.ChainTokenInfo.IsNullOrEmpty() &&
                t.ChainTokenInfo.Exists(c => c.ChainId == item.ChainId))?
                .ChainTokenInfo?.FirstOrDefault(c => c.ChainId == item.ChainId)?.Status;
            var userTokenIssueGrain = _clusterClient.GetGrain<IUserTokenIssueGrain>(
                GuidHelper.UniqGuid(input.Symbol, address, item.ChainId));
            var res = await userTokenIssueGrain.Get();
            item.TotalSupply = tokenOwner?.TotalSupply ?? 0;
            item.Decimals = tokenOwner?.Decimals ?? 0;
            item.TokenName = tokenOwner?.TokenName;
            item.ContractAddress = tokenOwner?.ContractAddress;
            item.Icon = tokenOwner?.Icon;
            item.Status = isCompleted
                ? TokenApplyOrderStatus.Complete.ToString()
                : !applyStatus.IsNullOrEmpty() 
                    ? applyStatus 
                    : res != null && !res.Status.IsNullOrEmpty()
                        ? res.Status 
                        : tokenOwner?.Status ?? TokenApplyOrderStatus.Unissued.ToString();
            item.Checked = isCompleted ||
                           applyOrderList.Exists(t => !t.ChainTokenInfo.IsNullOrEmpty() &&
                               t.ChainTokenInfo.Exists(c => c.ChainId == item.ChainId));
            if (res != null && !res.BindingId.IsNullOrEmpty() && !res.ThirdTokenId.IsNullOrEmpty())
            {
                item.BindingId = res.BindingId;
                item.ThirdTokenId = res.ThirdTokenId;
            }
        }

        foreach (var item in result.OtherChainList)
        {
            var isCompleted = _tokenInfoOptions.Value.ContainsKey(item.Symbol) &&
                              _tokenInfoOptions.Value[item.Symbol].Transfer.Contains(item.ChainId);
            var tokenOwner = listDto.TokenOwnerList.FirstOrDefault();
            var applyOrder = applyOrderList.FirstOrDefault(t => t.OtherChainTokenInfo != null &&
                t.OtherChainTokenInfo.ChainId == item.ChainId);
            var applyStatus = applyOrder?.OtherChainTokenInfo?.Status;
            var userTokenIssueGrain = _clusterClient.GetGrain<IUserTokenIssueGrain>(
                GuidHelper.UniqGuid(input.Symbol, address, item.ChainId));
            var res = await userTokenIssueGrain.Get();
            item.TotalSupply = res?.TotalSupply.SafeToDecimal() ?? 0M;
            item.Decimals = 0;
            item.TokenName = res?.TokenName ?? tokenOwner?.TokenName;
            item.ContractAddress = res?.ContractAddress;
            item.Icon = res?.TokenImage ?? tokenOwner?.Icon;
            item.Status = isCompleted
                ? TokenApplyOrderStatus.Complete.ToString()
                : !applyStatus.IsNullOrEmpty() 
                    ? applyStatus 
                    : res != null && !res.Status.IsNullOrEmpty()
                        ? res.Status 
                        : TokenApplyOrderStatus.Unissued.ToString();
            item.Checked = isCompleted ||
                           applyOrderList.Exists(t => t.OtherChainTokenInfo != null &&
                                                      t.OtherChainTokenInfo.ChainId == item.ChainId);
            if (res != null && !res.BindingId.IsNullOrEmpty() && !res.ThirdTokenId.IsNullOrEmpty())
            {
                item.BindingId = res.BindingId;
                item.ThirdTokenId = res.ThirdTokenId;
            }
            if (item.Status == TokenApplyOrderStatus.Rejected.ToString())
            {
                item.RejectedTime = applyOrder != null && applyOrder.StatusChangedRecord != null &&
                                    applyOrder.StatusChangedRecord.ContainsKey(TokenApplyOrderStatus.Rejected.ToString())
                    ? applyOrder.StatusChangedRecord[TokenApplyOrderStatus.Rejected.ToString()].SafeToLong()
                    : 0L;
            }
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
        
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(await GetTokenApplyOrderIndexListCountAsync(address, input.Symbol) > 0 ||
            (!input.ChainIds.IsNullOrEmpty() && !input.OtherChainIds.IsNullOrEmpty()), "Param invalid.");
        
        var result = new AddChainResultDto();
        if (!input.OtherChainIds.IsNullOrEmpty())
        {
            foreach (var item in input.OtherChainIds)
            {
                var chain = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == item);
                if (chain.Status != TokenApplyOrderStatus.Issued.ToString() &&
                    chain.Status != TokenApplyOrderStatus.Rejected.ToString()) continue;
                var orderId = GuidHelper.UniqGuid(input.Symbol, address, item);
                var tokenApplyOrderGrain = _clusterClient.GetGrain<IUserTokenApplyOrderGrain>(orderId);
                var applyOrder = await GetTokenApplyOrderIndexAsync(orderId.ToString());
                if (applyOrder != null && applyOrder.Status == TokenApplyOrderStatus.Rejected.ToString() &&
                    applyOrder.StatusChangedRecord != null &&
                    applyOrder.StatusChangedRecord.ContainsKey(TokenApplyOrderStatus.Rejected.ToString()))
                {
                    var time = applyOrder.StatusChangedRecord[TokenApplyOrderStatus.Rejected.ToString()].SafeToLong();
                    if (DateTime.UtcNow.ToUtcMilliSeconds() - time <= _tokenAccessOptions.Value.ReApplyHours * 3600000)
                        continue;
                }
                else if (await tokenApplyOrderGrain.Get() != null || applyOrder != null) continue;
                
                chain.Status = TokenApplyOrderStatus.Reviewing.ToString();
                var dto = new TokenApplyOrderDto
                {
                    Id = orderId,
                    Symbol = input.Symbol,
                    UserAddress = address,
                    Status = TokenApplyOrderStatus.Reviewing.ToString(),
                    ChainTokenInfo = input.ChainIds.IsNullOrEmpty() 
                        ? null 
                        : chainStatus.ChainList.Where(t => input.ChainIds.Exists(
                        c => c == t.ChainId) && t.Status == TokenApplyOrderStatus.Issued.ToString())
                        .ToList().ConvertAll(t => _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(t)),
                    OtherChainTokenInfo = _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(chain)
                };
                if (!dto.ChainTokenInfo.IsNullOrEmpty())
                {
                    dto.ChainTokenInfo.ForEach(t => t.Status = TokenApplyOrderStatus.PoolInitializing.ToString());
                }
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

        if (!input.ChainIds.IsNullOrEmpty())
        {
            foreach (var item in input.ChainIds)
            {
                var chain = chainStatus.ChainList.FirstOrDefault(t => t.ChainId == item);
                if (chain.Status != TokenApplyOrderStatus.Issued.ToString() || 
                    !input.OtherChainIds.IsNullOrEmpty()) return result;
                var orderId = GuidHelper.UniqGuid(input.Symbol, address, item);
                var tokenApplyOrderGrain = _clusterClient.GetGrain<IUserTokenApplyOrderGrain>(orderId);
                var applyOrder = await GetTokenApplyOrderIndexAsync(orderId.ToString());
                if (await tokenApplyOrderGrain.Get() != null || applyOrder != null) return result;
                
                chain.Status = TokenApplyOrderStatus.PoolInitializing.ToString();
                var dto = new TokenApplyOrderDto
                {
                    Id = orderId,
                    Symbol = input.Symbol,
                    UserAddress = address,
                    Status = TokenApplyOrderStatus.PoolInitializing.ToString(),
                    ChainTokenInfo = new List<ChainTokenInfoDto> { _objectMapper.Map<ChainAccessInfo, ChainTokenInfoDto>(chain) }
                };
                dto.StatusChangedRecord ??= new Dictionary<string, string>();
                dto.StatusChangedRecord.AddOrReplace(TokenApplyOrderStatus.PoolInitializing.ToString(),
                    DateTime.UtcNow.ToUtcMilliSeconds().ToString());
                await tokenApplyOrderGrain.AddOrUpdate(dto);
                result.ChainList ??= new List<AddChainDto>();
                result.ChainList.Add(new()
                {
                    Id = orderId.ToString(),
                    ChainId = item
                });
            }
        }

        if (!result.ChainList.IsNullOrEmpty() || !result.OtherChainList.IsNullOrEmpty())
        {
            var monitorGrain = _clusterClient.GetGrain<IUserTokenAccessMonitorGrain>(Guid.NewGuid().ToString());
            await monitorGrain.DoTokenListingMonitor(result);
        }
        return result;
    }

    public async Task<UserTokenBindingDto> PrepareBindingIssueAsync(PrepareBindIssueInput input)
    {
        AssertHelper.IsTrue(!input.ChainId.IsNullOrEmpty() || !input.OtherChainId.IsNullOrEmpty(), 
            "Param invalid.");
        var chainStatus = await CheckChainAccessStatusAsync(new CheckChainAccessStatusInput { Symbol = input.Symbol });
        AssertHelper.IsTrue(input.ChainId.IsNullOrEmpty() || chainStatus.ChainList.Exists(
            c => c.ChainId == input.ChainId), "Param invalid.");
        AssertHelper.IsTrue(input.OtherChainId.IsNullOrEmpty() || chainStatus.OtherChainList.Exists(
            c => c.ChainId == input.OtherChainId), "Param invalid.");

        var address = await GetUserAddressAsync();
        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(
            string.Join(CommonConstant.Underline, input.Symbol, address, input.OtherChainId));
        var dto = new UserTokenIssueDto
        {
            Id = GuidHelper.UniqGuid(input.Symbol, address, input.OtherChainId),
            Address = address,
            WalletAddress = input.Address,
            Symbol = input.Symbol,
            ChainId = input.ChainId,
            TokenName = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == input.OtherChainId)?.TokenName ??
                        chainStatus.ChainList.FirstOrDefault(t => t.ChainId == input.ChainId)?.TokenName,
            TokenImage = chainStatus.OtherChainList.FirstOrDefault(t => t.ChainId == input.OtherChainId)?.Icon ??
                         chainStatus.ChainList.FirstOrDefault(t => t.ChainId == input.ChainId)?.Icon,
            OtherChainId = input.OtherChainId,
            ContractAddress = input.ContractAddress,
            TotalSupply = input.Supply
        };
        return await tokenInvokeGrain.PrepareBinding(dto);
    }
    
    public async Task<bool> GetBindingIssueAsync(UserTokenBindingDto input)
    {
        var address = await GetUserAddressAsync();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), "No permission."); 
        
        var tokenInvokeGrain = _clusterClient.GetGrain<ITokenInvokeGrain>(
            string.Join(CommonConstant.Underline, input.BindingId, input.ThirdTokenId));
        return await tokenInvokeGrain.Binding(input);
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

    public async Task<PagedResultDto<TokenApplyOrderResultDto>> GetTokenApplyListAsync(GetTokenApplyOrderListInput input)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Status).Value(input.Status)));
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (count, list) = await _tokenApplyOrderIndexRepository.GetSortListAsync(Filter,
            sortFunc: s => s.Ascending(a => a.UpdateTime), 
            skip: input.SkipCount, limit: input.MaxResultCount);
        return new PagedResultDto<TokenApplyOrderResultDto>
        {
            Items = _objectMapper.Map<List<TokenApplyOrderIndex>, List<TokenApplyOrderResultDto>>(list),
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

            if (item.OtherChainTokenInfo != null)
            {
                if (item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.Reviewing.ToString() ||
                    item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.Reviewed.ToString() ||
                    item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.PoolInitializing.ToString() ||
                    item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.PoolInitialized.ToString() ||
                    item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.Integrating.ToString() ||
                    item.OtherChainTokenInfo.Status == TokenApplyOrderStatus.Complete.ToString())
                {
                    try
                    {
                        var coboGrain = _clusterClient.GetGrain<ICoBoAccountGrain>(GuidHelper.UniqGuid(
                            nameof(ICoBoAccountGrain)));
                        item.OtherChainTokenInfo.BalanceAmount = (await coboGrain.Get(ICoBoAccountGrain.Id(
                            item.OtherChainTokenInfo.ChainId, item.OtherChainTokenInfo.Symbol)))?.AbsBalance ?? "0";
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Get cobo account info fail.");
                        item.OtherChainTokenInfo.BalanceAmount = "0";
                    }
                }
                if (_networkInfoOptions.Value.NetworkMap.ContainsKey(item.OtherChainTokenInfo.Symbol) &&
                    _networkInfoOptions.Value.NetworkMap[item.OtherChainTokenInfo.Symbol].Exists(
                        t => t.NetworkInfo.Network == item.OtherChainTokenInfo.ChainId))
                {
                    var network = _networkInfoOptions.Value.NetworkMap[item.OtherChainTokenInfo.Symbol].FirstOrDefault(
                        t => t.NetworkInfo.Network == item.OtherChainTokenInfo.ChainId);
                    item.OtherChainTokenInfo.MinAmount = network.NetworkInfo?.MinAmount ?? "0";
                    item.OtherChainTokenInfo.PoolAddress = network.NetworkInfo?.PoolAddress;
                    item.OtherChainTokenInfo.Limit24HInUsd = network.WithdrawInfo?.WithdrawLimit24h ?? "0";
                    item.OtherChainTokenInfo.Decimals = network.WithdrawInfo?.Decimals ?? 0;
                    item.OtherChainTokenInfo.TokenContractAddress = network.NetworkInfo?.ContractAddress;
                }
            }

            if (!item.ChainTokenInfo.IsNullOrEmpty())
            {
                foreach (var chain in item.ChainTokenInfo)
                {
                    if (!_networkInfoOptions.Value.NetworkMap.ContainsKey(chain.Symbol) ||
                        !_networkInfoOptions.Value.NetworkMap[chain.Symbol].Exists(
                            t => t.NetworkInfo.Network == chain.ChainId)) continue;
                    var network = _networkInfoOptions.Value.NetworkMap[chain.Symbol].FirstOrDefault(
                        t => t.NetworkInfo.Network == chain.ChainId);
                    chain.MinAmount = network.NetworkInfo?.MinAmount ?? "0";
                    chain.PoolAddress = network.NetworkInfo?.PoolAddress;
                    chain.Limit24HInUsd = network.WithdrawInfo?.WithdrawLimit24h ?? "0";
                    chain.Decimals = network.WithdrawInfo?.Decimals ?? 0;
                    chain.TokenContractAddress = network.NetworkInfo?.ContractAddress;
                    if (chain.PoolAddress.IsNullOrEmpty()) continue;
                    if (chain.Status == TokenApplyOrderStatus.PoolInitializing.ToString() ||
                        chain.Status == TokenApplyOrderStatus.PoolInitialized.ToString() ||
                        chain.Status == TokenApplyOrderStatus.Integrating.ToString() ||
                        chain.Status == TokenApplyOrderStatus.Complete.ToString())
                    {
                        try
                        {
                            var balance = await _contractProvider.CallTransactionAsync<GetBalanceOutput>(chain.ChainId,
                                SystemContractName.TokenContract,
                                "GetBalance",
                                new GetBalanceInput
                                {
                                    Owner = Address.FromBase58(chain.PoolAddress),
                                    Symbol = chain.Symbol
                                });
                            if (balance.Balance == 0) continue;
                            var tokenGrain =
                                _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(chain.Symbol, chain.ChainId));
                            var token = await tokenGrain.GetToken();
                            var decimalPow = (decimal)Math.Pow(10, token.Decimals);
                            var balanceDecimal = balance.Balance / decimalPow;
                            chain.BalanceAmount = balanceDecimal.ToString();
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "GetBalance fail.");
                            chain.BalanceAmount = "0";
                        }
                    }
                }
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
    
    private bool CheckLiquidityAndHolderAvailable(List<TokenOwnerDto> TokenOwnerList, string symbol)
    {
        var tokenOwnerDto = TokenOwnerList.FirstOrDefault(t => t.Symbol == symbol);
        var liquidityInUsd = !_tokenAccessOptions.Value.TokenConfig.ContainsKey(symbol)
            ? _tokenAccessOptions.Value.DefaultConfig.Liquidity
            : _tokenAccessOptions.Value.TokenConfig[symbol].Liquidity;
        var holders = !_tokenAccessOptions.Value.TokenConfig.ContainsKey(symbol)
            ? _tokenAccessOptions.Value.DefaultConfig.Holders
            : _tokenAccessOptions.Value.TokenConfig[symbol].Holders;
        return tokenOwnerDto.LiquidityInUsd.SafeToDecimal() > liquidityInUsd.SafeToDecimal()
               && tokenOwnerDto.Holders > holders;
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
    
    private async Task<long> GetTokenApplyOrderIndexListCountAsync(string address, string symbol)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenApplyOrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserAddress).Value(address)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Symbol).Value(symbol)));
        QueryContainer Filter(QueryContainerDescriptor<TokenApplyOrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var result = await _tokenApplyOrderIndexRepository.CountAsync(Filter);
        return result.Count;
    }
}