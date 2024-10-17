using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Users;
using ETransferServer.User;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserDepositAddressGrain : IGrainWithStringKey
{
    Task<string> GetUserAddress(GetUserDepositAddressInput input);
    Task<bool> Exist();
    Task<string> GetAddress();
    Task<CommonResponseDto<UserDepositAddressState>> AddOrUpdate(UserAddressDto dto);
}

public class UserDepositAddressGrain : Grain<UserDepositAddressState>, IUserDepositAddressGrain
{
    private readonly IUserAddressProvider _userAddressProvider;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UserDepositAddressGrain> _logger;

    public UserDepositAddressGrain(IUserAddressProvider userAddressProvider,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOptions,
        IObjectMapper objectMapper, ILogger<UserDepositAddressGrain> logger)
    {
        _userAddressProvider = userAddressProvider;
        _depositAddressOptions = depositAddressOptions;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<string> GetUserAddress(GetUserDepositAddressInput input)
    {
        var exist = await Exist();
        if (exist)
        {
            return State.UserToken.Address;
        }
        
        var evmCoins = _depositAddressOptions.Value.EVMCoins;
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

    public async Task<CommonResponseDto<UserDepositAddressState>> AddOrUpdate(UserAddressDto dto)
    {
        State = _objectMapper.Map<UserAddressDto, UserDepositAddressState>(dto);
        await WriteStateAsync();

        return new CommonResponseDto<UserDepositAddressState>()
        {
            Data = State
        };
    }

    private async Task<IUserDepositAddressGrain> GetUserDepositGrainAsync(string coin, GetUserDepositAddressInput input)
    {
        var split = coin.Split(CommonConstant.Underline);
        return split.Length >= 2 && !(split[0] == input.NetWork && split[1] == input.Symbol)
            ? GetUserDepositGrain(input, split)
            : null;
    }

    private IUserDepositAddressGrain GetUserDepositGrain(GetUserDepositAddressInput input, string[] split)
    {
        if (DepositSwapHelper.NoDepositSwap(input.Symbol, input.ToSymbol))
        {
            return GrainFactory.GetGrain<IUserDepositAddressGrain>(GuidHelper.GenerateGrainId(input.UserId,
                input.ChainId, split[0], split[1]));
        }

        return GrainFactory.GetGrain<IUserDepositAddressGrain>(GuidHelper.GenerateGrainId(input.UserId,
            input.ChainId, split[0], split[1], input.ToSymbol));
    }

    private async Task<string> HandleUpdateAsync(GetUserDepositAddressInput input, IUserDepositAddressGrain grain)
    {
        var addressDto = await _userAddressProvider.GetUserUnAssignedAddressAsync(input);
        if (addressDto == null) return null;
        addressDto.UserId = input.UserId;
        addressDto.ChainId = input.ChainId;
        addressDto.IsAssigned = true;
        addressDto.UpdateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow);
        var isDepositSwap = DepositSwapHelper.IsDepositSwap(input.Symbol, input.ToSymbol);
        addressDto.FromSymbol = isDepositSwap ? input.Symbol : addressDto.UserToken.Symbol;
        addressDto.ToSymbol = isDepositSwap ? input.ToSymbol : addressDto.UserToken.Symbol;

        await grain.AddOrUpdate(addressDto);
        var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(addressDto.UserToken.Address);
        await addressGrain.AddOrUpdate(addressDto);
        await _userAddressProvider.UpdateSync(addressDto);

        return addressDto.UserToken.Address;
    }
}