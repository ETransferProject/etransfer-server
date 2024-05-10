using ETransferServer.Dtos.Order;
using Orleans;

namespace ETransferServer.Grains.Grain.Swap;

public interface ISwapGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<DepositOrderChangeDto>> SwapAsync(DepositOrderDto dto);
    // Task<GrainResultDto<DepositOrderChangeDto>> SubsidyTransferAsync(DepositOrderDto dto，string returnValue);
    Task<decimal> ParseReturnValueAsync(string returnValue);
}