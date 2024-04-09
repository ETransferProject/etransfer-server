using ETransferServer.Common;
using Orleans;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Users;
using ETransferServer.User;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserDepositAddressGrain : IGrainWithStringKey
{
    Task<string> GetUserAddress(GetUserDepositAddressInput input);
    Task<bool> Exist();
    Task<string> GetAddress();
    Task<CommonResponseDto<TokenDepositAddressState>> AddOrUpdate(UserAddressDto dto);
}

public class UserDepositAddressGrain : Grain<TokenDepositAddressState>, IUserDepositAddressGrain
{
    private readonly IUserAddressProvider _userAddressProvider;
    private readonly IOptionsMonitor<DepositAddressOptions> _depositAddressOptions;
    private readonly IObjectMapper _objectMapper;

    public UserDepositAddressGrain(IUserAddressProvider userAddressProvider,
        IOptionsMonitor<DepositAddressOptions> depositAddressOptions,
        IObjectMapper objectMapper)
    {
        _userAddressProvider = userAddressProvider;
        _depositAddressOptions = depositAddressOptions;
        _objectMapper = objectMapper;
    }

    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }

    public async Task<string> GetUserAddress(GetUserDepositAddressInput input)
    {
        var exist = await Exist();
        if (exist)
        {
            return State.UserToken.Address;
        }
        
        var evmCoins = _depositAddressOptions.CurrentValue.EVMCoins;
        if (evmCoins.Contains(GuidHelper.GenerateId(input.NetWork, input.Symbol)) && evmCoins.Count > 0)
        {
            foreach (var coin in evmCoins)
            {
                var userGrain = await GetUserDepositGrainAsync(coin, input);
                if(userGrain == null) continue;
                exist = await userGrain.Exist();
                if (exist)
                {
                    return await userGrain.GetAddress();
                }
            }
            var firstGrain = await GetUserDepositGrainAsync(evmCoins.First(), input);
            if (firstGrain != null)
            {
                return await HandleUpdateAsync(input, firstGrain);
            }
        }
        return await HandleUpdateAsync(input, this);
    }
    
    public Task<bool> Exist()
    {
        return Task.FromResult(State.UserToken != null && !string.IsNullOrEmpty(State.UserId) && !string.IsNullOrEmpty(State.ChainId) &&
                    !string.IsNullOrEmpty(State.UserToken.ChainId) && !string.IsNullOrEmpty(State.UserToken.Symbol));
    }

    public Task<string> GetAddress()
    {
        return Task.FromResult(State.UserToken.Address);
    }

    public async Task<CommonResponseDto<TokenDepositAddressState>> AddOrUpdate(UserAddressDto dto)
    {
        State = _objectMapper.Map<UserAddressDto, TokenDepositAddressState>(dto);
        await WriteStateAsync();

        return new CommonResponseDto<TokenDepositAddressState>()
        {
            Data = State
        };
    }

    private async Task<IUserDepositAddressGrain> GetUserDepositGrainAsync(string coin, GetUserDepositAddressInput input)
    {
        var split = coin.Split(DepositAddressOptions.DefaultDelimiter);
        return split.Length >= 2 && !(split[0] == input.NetWork && split[1] == input.Symbol) ?
            GrainFactory.GetGrain<IUserDepositAddressGrain>(GuidHelper.GenerateGrainId(input.UserId,
                    input.ChainId, split[0], split[1]))
            : null;
    }

    private async Task<string> HandleUpdateAsync(GetUserDepositAddressInput input, IUserDepositAddressGrain grain)
    {
        var addressDto = await _userAddressProvider.GetUserUnAssignedAddressAsync(input);
        if (addressDto == null) return null;
        addressDto.UserId = input.UserId;
        addressDto.ChainId = input.ChainId;
        addressDto.IsAssigned = true;
        addressDto.UpdateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow);
        
        await grain.AddOrUpdate(addressDto);
        var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(addressDto.UserToken.Address);
        await addressGrain.AddOrUpdate(addressDto);
        await _userAddressProvider.UpdateSync(addressDto);

        return addressDto.UserToken.Address;
    }
}