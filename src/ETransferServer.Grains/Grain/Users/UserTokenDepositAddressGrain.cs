using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.State.Users;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenDepositAddressGrain : IGrainWithStringKey
{
    Task<CommonResponseDto<UserAddressDto>> Get();
    Task<bool> Exist();
    Task<CommonResponseDto<TokenDepositAddressState>> AddOrUpdate(UserAddressDto dto);
}

public class UserTokenDepositAddressGrain : Grain<TokenDepositAddressState>, IUserTokenDepositAddressGrain
{
    private readonly IObjectMapper _objectMapper;

    public UserTokenDepositAddressGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
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

    public async Task<CommonResponseDto<UserAddressDto>> Get()
    {
        return State.Id == Guid.Empty
            ? new CommonResponseDto<UserAddressDto>(null)
            : new CommonResponseDto<UserAddressDto>(
            _objectMapper.Map<TokenDepositAddressState, UserAddressDto>(State));
    }
    
    public Task<bool> Exist()
    {
        return Task.FromResult(State.Id != Guid.Empty);
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
}