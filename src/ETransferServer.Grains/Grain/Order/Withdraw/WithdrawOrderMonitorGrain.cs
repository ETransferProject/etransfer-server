using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IWithdrawOrderMonitorGrain : IGrainWithStringKey
{
    Task DoMonitor(WithdrawOrderMonitorDto dto);
    Task DoLargeAmountMonitor(WithdrawOrderDto dto);
    Task DoCallbackMonitor(TransferOrderMonitorDto dto);
}

public class WithdrawOrderMonitorGrain : Grain<WithdrawOrderMonitorState>, IWithdrawOrderMonitorGrain
{
    private const string WithdrawOrderFailureAlarm = "WithdrawOrderFailureAlarm";
    private const string WithdrawLargeAmountAlarm = "WithdrawLargeAmountAlarm";
    private const string TransferCallbackAlarm = "TransferCallbackAlarm";

    private readonly ILogger<WithdrawOrderMonitorGrain> _logger;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;

    public WithdrawOrderMonitorGrain(ILogger<WithdrawOrderMonitorGrain> logger, 
        IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        IEnumerable<INotifyProvider> notifyProvider)
    {
        _logger = logger;
        _withdrawOptions = withdrawOptions;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }
    
    public async Task DoMonitor(WithdrawOrderMonitorDto dto)
    {
        try
        {
            if ((State != null && State.Id == dto.Id) || await ExistWithdrawOrderAsync(dto))
            {
                _logger.LogWarning("WithdrawOrderMonitor already handle: {id}", dto.Id);
                return;
            }
            var sendSuccess =
                await SendNotifyAsync(dto);
            AssertHelper.IsTrue(sendSuccess, "Send notify failed");
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "WithdrawOrderMonitor handle failed , Message={Msg}, GrainId={GrainId} dto={dto}",
                e.Message, this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "WithdrawOrderMonitor handle failed GrainId={GrainId}, dto={dto}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        finally
        {
            State.Id = dto.Id;
            await WriteStateAsync();
        }
    }

    public async Task DoLargeAmountMonitor(WithdrawOrderDto dto)
    {
        try
        {
            var symbolExists = _withdrawOptions.Value.LargeAmount.TryGetValue(dto.FromTransfer.Symbol, out var amount);
            AssertHelper.IsTrue(symbolExists, "Symbol not found: {Symbol}", dto.FromTransfer.Symbol);
            if (dto.FromTransfer.Amount >= amount)
            {
                var sendSuccess = await SendNotifyAsync(dto);
                AssertHelper.IsTrue(sendSuccess, "Send notify failed");
            }
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "WithdrawOrderMonitor largeAmount failed , Message={Msg}, GrainId={GrainId}",
                e.Message, this.GetPrimaryKeyString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "WithdrawOrderMonitor largeAmount failed GrainId={GrainId}",
                this.GetPrimaryKeyString());
        }
    }
    
    public async Task DoCallbackMonitor(TransferOrderMonitorDto dto)
    {
        try
        {
            if (State != null && State.Id == dto.Id)
            {
                _logger.LogWarning("TransferCallbackMonitor already handle: {id}", dto.Id);
                return;
            }
            var sendSuccess =
                await SendNotifyAsync(dto);
            AssertHelper.IsTrue(sendSuccess, "Send notify failed");
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "TransferCallbackMonitor handle failed , Message={Msg}, GrainId={GrainId} dto={dto}",
                e.Message, this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransferCallbackMonitor handle failed GrainId={GrainId}, dto={dto}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        finally
        {
            State.Id = dto.Id;
            await WriteStateAsync();
        }
    }

    private async Task<bool> ExistWithdrawOrderAsync(WithdrawOrderMonitorDto dto)
    {
        try
        {
            var orderId = OrderIdHelper.WithdrawOrderId(dto.Id, dto.ToChainId, dto.ToAddress);
            var withdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
            return  (await withdrawRecordGrain.Get())?.Value != null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "WithdrawOrderMonitor Exception, {id}", dto.Id);
        }

        return false;
    }
    
    private async Task<bool> SendNotifyAsync(WithdrawOrderMonitorDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = WithdrawOrderFailureAlarm,
            Params = new Dictionary<string, string>
            {
                [Keys.OrderType] = OrderTypeEnum.Withdraw.ToString(),
                [Keys.TransactionId] = dto.TransactionId,
                [Keys.MethodName] = dto.MethodName,
                [Keys.From] = dto.From,
                [Keys.To] = dto.To,
                [Keys.ToChainId] = dto.ToChainId,
                [Keys.ToAddress] = dto.ToAddress,
                [Keys.Symbol] = dto.Symbol,
                [Keys.Amount] = dto.Amount.ToString(),
                [Keys.MaxEstimateFee] = dto.MaxEstimateFee.ToString(),
                [Keys.Timestamp] = dto.Timestamp.ToString(),
                [Keys.ChainId] = dto.ChainId,
                [Keys.BlockHash] = dto.BlockHash,
                [Keys.BlockHeight] = dto.BlockHeight.ToString(),
                [Keys.Reason] = dto.Reason,
            }
        });
    }

    private async Task<bool> SendNotifyAsync(WithdrawOrderDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = WithdrawLargeAmountAlarm,
            Params = new Dictionary<string, string>
            {
                [LargeAmountKeys.OrderType] = OrderTypeEnum.Withdraw.ToString(),
                [LargeAmountKeys.OrderId] = dto.Id.ToString(),
                [LargeAmountKeys.CreateTime] = dto.CreateTime.HasValue && dto.CreateTime.Value > 0
                    ? DateTimeHelper.FromUnixTimeMilliseconds(dto.CreateTime.Value).ToUtc8String()
                    : DateTime.UtcNow.ToUtc8String(),
                [LargeAmountKeys.Amount] = dto.FromTransfer.Amount.ToString(),
                [LargeAmountKeys.Symbol] = dto.FromTransfer.Symbol,
                [LargeAmountKeys.TxId] = dto.FromTransfer.TxId,
                [LargeAmountKeys.ToAddress] = dto.ToTransfer.ToAddress,
                [LargeAmountKeys.ToNetwork] = dto.ToTransfer.Network == CommonConstant.Network.AElf 
                    ? dto.ToTransfer.ChainId
                    : dto.ToTransfer.Network
            }
        });
    }
    
    private async Task<bool> SendNotifyAsync(TransferOrderMonitorDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = TransferCallbackAlarm,
            Params = new Dictionary<string, string>
            {
                [CallbackKeys.OrderType] = dto.OrderType,
                [CallbackKeys.OrderId] = dto.OrderId ?? dto.Id,
                [CallbackKeys.FromNetwork] = dto.FromNetwork,
                [CallbackKeys.ToNetwork] = dto.ToNetwork,
                [CallbackKeys.Amount] = dto.Amount,
                [CallbackKeys.Symbol] = dto.Symbol,
                [CallbackKeys.Reason] = dto.Reason
            }
        });
    }

    private static class Keys
    {
        public const string OrderType = "orderType";
        public const string TransactionId = "transactionId";
        public const string MethodName = "methodName";
        public const string From = "from";
        public const string To = "to";
        public const string ToChainId = "toChainId";
        public const string ToAddress = "toAddress";
        public const string Symbol = "symbol";
        public const string Amount = "amount";
        public const string MaxEstimateFee = "maxEstimateFee";
        public const string Timestamp = "timestamp";
        public const string ChainId = "chainId";
        public const string BlockHash = "blockHash";
        public const string BlockHeight = "blockHeight";
        public const string Reason = "reason";
    }
    
    private static class LargeAmountKeys
    {
        public const string OrderType = "orderType";
        public const string OrderId = "orderId";
        public const string CreateTime = "createTime";
        public const string Amount = "amount";
        public const string Symbol = "symbol";
        public const string TxId = "txId";
        public const string ToAddress = "toAddress";
        public const string ToNetwork = "toNetwork";
    }
    
    private static class CallbackKeys
    {
        public const string OrderType = "orderType";
        public const string OrderId = "orderId";
        public const string FromNetwork = "fromNetwork";
        public const string ToNetwork = "toNetwork";
        public const string Amount = "amount";
        public const string Symbol = "symbol";
        public const string Reason = "reason";
    }
}