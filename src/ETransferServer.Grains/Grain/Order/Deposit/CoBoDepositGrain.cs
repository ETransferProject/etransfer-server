using ETransferServer.Common;
using ETransferServer.Grains.State.Order;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface ICoBoDepositGrain : IGrainWithStringKey
{
    Task AddOrUpdate(CoBoTransactionDto dto);
    Task<CoBoTransactionDto> Get();

    Task<bool> NeedUpdate();

    Task<bool> NotUpdated();
}

public class CoBoDepositGrain : Grain<CoBoTransactionState>, ICoBoDepositGrain
{
    private readonly IObjectMapper _objectMapper;

    public CoBoDepositGrain(IObjectMapper objectMapper)
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

    public async Task AddOrUpdate(CoBoTransactionDto dto)
    {
        int updateCount = State.UpdateCount;
        State = _objectMapper.Map<CoBoTransactionDto, CoBoTransactionState>(dto);
        State.UpdateCount = updateCount + 1;
        await WriteStateAsync();
    }

    public async Task<CoBoTransactionDto> Get()
    {
        return _objectMapper.Map<CoBoTransactionState, CoBoTransactionDto>(State);
    }

    public async Task<bool> NeedUpdate()
    {
        return State == null || State?.Id == null || State.Status != CommonConstant.SuccessStatus;
    }
    
    public async Task<bool> NotUpdated()
    {
        return State == null || State?.UpdateCount <= 0;
    }
}