using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order;

public interface IOrderStatusFlowGrain : IGrainWithGuidKey
{
    public Task<CommonResponseDto<OrderStatusFlowDto>> AddAsync(string status, Dictionary<string, string> externalInfo);

    public Task<CommonResponseDto<OrderStatusFlowDto>> GetAsync();
}

public class OrderStatusFlowGrain : Grain<OrderStatusFlowState>, IOrderStatusFlowGrain
{
    private const int MaxFlowCount = 200;
    private readonly IObjectMapper _objectMapper;

    public OrderStatusFlowGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }


    public async Task<CommonResponseDto<OrderStatusFlowDto>> AddAsync(string status,
        Dictionary<string, string> externalInfo)
    {
        State.Id = this.GetPrimaryKey();

        var flowItem = new OrderStatus
        {
            Status = status,
            LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds(),
        };
        if (!externalInfo.IsNullOrEmpty())
        {
            flowItem.Extension = externalInfo;
        }

        if (State.StatusFlow.Count >= MaxFlowCount)
        {
            return new CommonResponseDto<OrderStatusFlowDto>().Error("Max status count exceeded");
        }

        State.StatusFlow.Add(flowItem);
        await WriteStateAsync();

        return new CommonResponseDto<OrderStatusFlowDto>(
            _objectMapper.Map<OrderStatusFlowState, OrderStatusFlowDto>(State));
    }

    public Task<CommonResponseDto<OrderStatusFlowDto>> GetAsync()
    {
        var dto = _objectMapper.Map<OrderStatusFlowState, OrderStatusFlowDto>(State);
        return Task.FromResult(new CommonResponseDto<OrderStatusFlowDto>(dto));
    }
}