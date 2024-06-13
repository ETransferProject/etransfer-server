using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IWithdrawOrderMonitorGrain : IGrainWithStringKey
{
    Task DoMonitorAsync(WithdrawOrderMonitorDto dto);
}

public class WithdrawOrderMonitorGrain : Grain<WithdrawOrderMonitorState>, IWithdrawOrderMonitorGrain
{
    private const string WithdrawOrderFailureAlarm = "WithdrawOrderFailureAlarm";

    private readonly ILogger<WithdrawOrderMonitorGrain> _logger;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;

    public WithdrawOrderMonitorGrain(ILogger<WithdrawOrderMonitorGrain> logger, 
        IEnumerable<INotifyProvider> notifyProvider)
    {
        _logger = logger;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }
    
    public async Task DoMonitorAsync(WithdrawOrderMonitorDto dto)
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
    
    private async Task<bool> ExistWithdrawOrderAsync(WithdrawOrderMonitorDto dto)
    {
        try
        {
            var orderId = OrderIdHelper.WithdrawOrderId(dto.Id, dto.ToChainId, dto.ToAddress);
            var withdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
            return  (await withdrawRecordGrain.GetAsync())?.Value != null;
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
}