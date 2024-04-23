using ETransferServer.Common;
using Orleans;
using ETransferServer.Grains.State.Users;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserOrderActionGrain : IGrainWithGuidKey
{
    Task AddOrUpdate();
    Task<long> Get();
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

    public async Task AddOrUpdate()
    {
        State.Id = this.GetPrimaryKey();
        State.LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds();
        
        await WriteStateAsync();
    }

    public Task<long> Get()
    {
        return Task.FromResult(State.Id == Guid.Empty ? 0L : State.LastModifyTime);
    }
}