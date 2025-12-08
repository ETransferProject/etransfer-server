using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface ITransactionGrain : IGrainWithStringKey
{
    Task<GrainResultDto> Create();
}

public class TransactionGrain : Grain<TransactionState>, ITransactionGrain
{
    private readonly ILogger<TransactionGrain> _logger;

    public TransactionGrain(ILogger<TransactionGrain> logger)
    {
        _logger = logger;
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

    public async Task<GrainResultDto> Create()
    {
        var result = new GrainResultDto();
        if (!State.Id.IsNullOrEmpty())
        {
            result.Success = false;
            result.Message = "transaction already exists.";

            _logger.LogWarning("transaction already exists, id:{id}, createTime:{createTime}", State.Id,
                State.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"));
            return result;
        }

        State.Id = this.GetPrimaryKeyString();
        State.CreateTime = DateTime.UtcNow;

        _logger.LogInformation("transaction hash add success, id:{id}, createTime:{createTime}", State.Id,
            State.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        await WriteStateAsync();
        return result;
    }
}