using Orleans;
using ETransferServer.Grains.State.Users;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserOrderChangeGrain : IGrainWithStringKey
{
    Task AddOrUpdate(long? time);
    Task<long> Get();
}

public class UserOrderChangeGrain : Grain<UserOrderChangeState>, IUserOrderChangeGrain
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

    public async Task AddOrUpdate(long? time)
    {
        if (State.Id == null || State.Id == Guid.Empty)
        {
            State.Id = Guid.NewGuid();
            State.Address = this.GetPrimaryKeyString();
        }

        State.Time = time;
        await WriteStateAsync();
    }

    public async Task<long> Get()
    {
        return State?.Time ?? 0L;
    }
}