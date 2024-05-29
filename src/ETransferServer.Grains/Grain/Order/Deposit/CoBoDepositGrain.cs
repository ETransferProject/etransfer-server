using ETransferServer.Grains.State.Order;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface ICoBoDepositGrain : IGrainWithStringKey
{
    Task AddOrUpdate(CoBoTransactionDto dto);
    Task<CoBoTransactionDto> Get();

    Task<bool> NotExistAsync();

    Task<bool> NotUpdatedAsync();
}

public class CoBoDepositGrain : Grain<CoBoTransactionState>, ICoBoDepositGrain
{
    private readonly IObjectMapper _objectMapper;

    public CoBoDepositGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

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

    public async Task<bool> NotExistAsync()
    {
        return State == null || State?.Id == null;
    }
    
    public async Task<bool> NotUpdatedAsync()
    {
        return State == null || State?.UpdateCount <= 0;
    }
}