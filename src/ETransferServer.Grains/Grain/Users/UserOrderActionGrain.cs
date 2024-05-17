using ETransferServer.Common;
using Orleans;
using ETransferServer.Grains.State.Users;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserOrderActionGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync();
    Task<long> GetAsync();
}

public class UserOrderActionGrain : Grain<UserOrderActionState>, IUserOrderActionGrain
{
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

    public async Task AddOrUpdateAsync()
    {
        if (State.Id == null || State.Id == Guid.Empty)
        {
            State.Id = Guid.NewGuid();
            State.UserId = this.GetPrimaryKeyString();
        }

        State.LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds();
        await WriteStateAsync();
    }

    public async Task<long> GetAsync()
    {
        return State.Id == Guid.Empty ? -1 : State.LastModifyTime;
    }
}