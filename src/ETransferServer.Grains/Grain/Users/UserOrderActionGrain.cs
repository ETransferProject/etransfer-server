using ETransferServer.Common;
using Orleans;
using ETransferServer.Grains.State.Users;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserOrderActionGrain : IGrainWithStringKey
{
    Task AddOrUpdate(long? createTime);
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

    public async Task AddOrUpdate(long? createTime)
    {
        if (State.Id == null || State.Id == Guid.Empty)
        {
            State.Id = Guid.NewGuid();
            State.UserId = this.GetPrimaryKeyString();
        }

        if (createTime != null && createTime > State.LastModifyTime)
        {
            State.LastModifyTime = createTime.Value;
        }
        await WriteStateAsync();
    }

    public async Task<long> Get()
    {
        return State.Id == null || State.Id == Guid.Empty ? -1 : State.LastModifyTime;
    }
}