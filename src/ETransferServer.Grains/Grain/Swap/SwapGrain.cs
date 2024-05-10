using ETransferServer.Dtos.Order;
using Orleans;
using ETransferServer.Grains.State.Swap;

namespace ETransferServer.Grains.Grain.Swap;

public class SwapGrain : Grain<SwapState>, ISwapGrain
{
    public Task<GrainResultDto<DepositOrderChangeDto>> SwapAsync(DepositOrderDto dto)
    {
        throw new NotImplementedException();
    }
    
}