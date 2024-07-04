using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Orleans;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Users;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.User;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class UserAddressService : ApplicationService, IUserAddressService
{
    private readonly INESTRepository<UserAddress, Guid> _userAddressIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOptions;
    private readonly ILogger<UserAddressService> _logger;

    public UserAddressService(INESTRepository<UserAddress, Guid> userAddressIndexRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOptions,
        ILogger<UserAddressService> logger)
    {
        _userAddressIndexRepository = userAddressIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _depositAddressOptions = depositAddressOptions;
        _logger = logger;
    }

    public async Task<string> GetUserAddressAsync(GetUserDepositAddressInput input)
    {
        if (CurrentUser.GetId() == Guid.Empty)
        {
            throw new UserFriendlyException("Request invalid. Please refresh and try again.");
        }

        input.UserId = CurrentUser.GetId().ToString();
        var userGrain =
            _clusterClient.GetGrain<IUserDepositAddressGrain>(GenerateGrainId(input));
        var address = await userGrain.GetUserAddress(input);
        if (string.IsNullOrEmpty(address))
        {
            _logger.LogError("Assign address fail: {userId},{chainId},{netWork},{symbol}", input.UserId, input.ChainId,
                input.NetWork, input.Symbol);
            throw new UserFriendlyException("Assign address fail. Please refresh and try again.");
        }
        
        _logger.LogInformation("Assign address: {userId},{chainId},{netWork},{symbol},{address}", input.UserId, 
            input.ChainId, input.NetWork, input.Symbol, address);
        return address;
    }
    
    private string GenerateGrainId(GetUserDepositAddressInput input)
    {
        if (DepositSwapHelper.NoDepositSwap(input.Symbol, input.ToSymbol))
        {
            return GuidHelper.GenerateGrainId(input.UserId, input.ChainId,
                input.NetWork, input.Symbol);
        }
        return GuidHelper.GenerateGrainId(input.UserId, input.ChainId,
            input.NetWork, input.Symbol, input.ToSymbol);
    }

    public async Task<UserAddressDto> GetUnAssignedAddressAsync(GetUserDepositAddressInput input)
    {
        var evmCoins = _depositAddressOptions.Value.EVMCoins;
        if (evmCoins.Contains(GuidHelper.GenerateId(input.NetWork, input.Symbol)) && evmCoins.Count > 0)
        {
            // first get new address from non-first evm lists of the config, then get from first evm of the config.
            var evmsList = evmCoins.FindAll(e => e != evmCoins.First()).ConvertAll(e => e.Split(CommonConstant.Underline)[0]);
            var firstList = new List<string> { evmCoins.First().Split(CommonConstant.Underline)[0] };
            var firstSymbol = evmCoins.First().Split(CommonConstant.Underline)[1];
            return evmsList.Count > 0 ? 
                await GetNewAddressAsync(evmsList, firstSymbol)?? await GetNewAddressAsync(firstList, firstSymbol) 
                : await GetNewAddressAsync(firstList, firstSymbol);
        }
        
        return await GetNewAddressAsync(new List<string>{ input.NetWork }, input.Symbol);
    }

    public async Task<UserAddressDto> GetAssignedAddressAsync(string address)
    {
        if (address.IsNullOrWhiteSpace()) return null;
        var mustQuery = new List<Func<QueryContainerDescriptor<UserAddress>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i => i.Field(t => t.UserToken.Address).Value(address)));
        QueryContainer Filter(QueryContainerDescriptor<UserAddress> f) => f.Bool(b => b.Must(mustQuery));

        var userAddress = await _userAddressIndexRepository.GetAsync(Filter);
        return _objectMapper.Map<UserAddress, UserAddressDto>(userAddress);
    }
    
    public async Task<List<UserAddressDto>> GetAddressListAsync(List<string> addressList)
    {
        if (addressList.IsNullOrEmpty()) return null;
        var mustQuery = new List<Func<QueryContainerDescriptor<UserAddress>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(t => t.UserToken.Address).Terms(addressList)));
        QueryContainer Filter(QueryContainerDescriptor<UserAddress> f) => f.Bool(b => b.Must(mustQuery));

        var result = await _userAddressIndexRepository.GetListAsync(Filter, skip:0, limit: UserAddressOptions.QueryLimit);
        return _objectMapper.Map<List<UserAddress>, List<UserAddressDto>>(result.Item2);
    }

    public async Task<bool> BulkAddOrUpdateAsync(List<UserAddressDto> dtoList)
    {
        try
        {
            await _userAddressIndexRepository.BulkAddOrUpdateAsync(
                _objectMapper.Map<List<UserAddressDto>, List<UserAddress>>(dtoList));
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk save userAddressIndex fail: {count},{Message}", dtoList.Count, ex.Message);
            return false;
        }

        return true;
    }

    public async Task<bool> AddOrUpdateAsync(UserAddressDto dto)
    {
        try
        {
            await _userAddressIndexRepository.AddOrUpdateAsync(_objectMapper.Map<UserAddressDto, UserAddress>(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError("Save userAddressIndex fail: {id},{message}", dto.Id, ex.Message);
            return false;
        }

        return true;
    }

    public async Task<List<string>> GetRemainingAddressListAsync()
    {
        var remainingList = new List<string>();
        var evmCount = 0L;
        var evmCoins = _depositAddressOptions.Value.EVMCoins;
        
        foreach (var item in _depositAddressOptions.Value.SupportCoins)
        {
            var split = item.Split(CommonConstant.Underline);
            if (split.Length < 2) continue;

            var mustQuery = new List<Func<QueryContainerDescriptor<UserAddress>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i => i.Field(f => f.UserToken.ChainId).Value(split[0])));
            mustQuery.Add(q => q.Term(i => i.Field(f => f.UserToken.Symbol).Value(split[1])));
            mustQuery.Add(q => q.Term(i => i.Field(f => f.IsAssigned).Value(false)));

            QueryContainer Filter(QueryContainerDescriptor<UserAddress> f) => f.Bool(b => b.Must(mustQuery));
            var countResponse = await _userAddressIndexRepository.CountAsync(Filter);
            if (evmCoins.Contains(item))
            {
                evmCount += countResponse.Count;
                continue;
            }

            _logger.LogInformation("Remaining address count:{count}, coin:{coin}", countResponse.Count, item);
            if (countResponse.Count < _depositAddressOptions.Value.RemainingThreshold)
            {
                remainingList.Add(item);
            }
        }

        _logger.LogInformation("Remaining address count:{count}, coin:evms", evmCount);
        if (evmCount < _depositAddressOptions.Value.RemainingThreshold && evmCoins.Count > 0)
        {
            remainingList.Add(evmCoins.First());
        }

        return remainingList;
    }

    private async Task<UserAddressDto> GetNewAddressAsync(List<string> chainIdList, string symbol)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserAddress>, QueryContainer>>();

        mustQuery.Add(q => q.Terms(i => i.Field(f => f.UserToken.ChainId).Terms(chainIdList)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.UserToken.Symbol).Value(symbol)));
        mustQuery.Add(q => q.Term(i => i.Field(f => f.IsAssigned).Value(false)));

        QueryContainer Filter(QueryContainerDescriptor<UserAddress> f) => f.Bool(b => b.Must(mustQuery));

        var list = await _userAddressIndexRepository.GetSortListAsync(Filter, null,
            s => s.Ascending(a => a.CreateTime) , 1);
        if (list.Item2.Count == 0) return null;

        return _objectMapper.Map<UserAddress, UserAddressDto>(list.Item2[0]);
    }
}