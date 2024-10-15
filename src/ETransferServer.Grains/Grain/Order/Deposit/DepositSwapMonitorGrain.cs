using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface IDepositSwapMonitorGrain : IGrainWithStringKey
{
    Task<bool> DoMonitor(DepositSwapMonitorDto depositSwapMonitorDto);
}

public class DepositSwapMonitorGrain : Grain<DepositSwapMonitorState>, IDepositSwapMonitorGrain
{
    private const string DepositOrderSwapFailureAlarm = "DepositOrderSwapFailureAlarm";

    private readonly ILogger<DepositSwapMonitorGrain> _logger;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;

    public DepositSwapMonitorGrain(ILogger<DepositSwapMonitorGrain> logger, IEnumerable<INotifyProvider> notifyProvider)
    {
        _logger = logger;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }
    
    [ExceptionHandler(typeof(UserFriendlyException), typeof(Exception),
        TargetType = typeof(DepositSwapMonitorGrain), MethodName = nameof(HandleExceptionAsync))]
    public async Task<bool> DoMonitor(DepositSwapMonitorDto dto)
    {
        var sendSuccess =
            await SendNotifyAsync(dto);
        AssertHelper.IsTrue(sendSuccess, "Send notify failed");
        return true;
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, DepositSwapMonitorDto dto)
    {
        if (ex is UserFriendlyException)
        {
            _logger.LogWarning(
                "Deposit swap monitor handle failed , Message={Msg}, GrainId={GrainId} dto={dto}", ex.Message,
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        else
        {
            _logger.LogError(ex, "Deposit swap monitor handle failed GrainId={GrainId}, feeInfo={dto}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }

        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    private async Task<bool> SendNotifyAsync(DepositSwapMonitorDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = DepositOrderSwapFailureAlarm,
            Params = new Dictionary<string, string>
            {
                [Keys.OrderId] = dto.OrderId,
                [Keys.OrderType] = dto.OrderType,
                [Keys.UserId] = dto.UserId,
                [Keys.TxId] = dto.txId,
                [Keys.SymbolFrom] = dto.FromSymbol,
                [Keys.SymbolTo] = dto.ToSymbol,
                [Keys.NetworkFrom] = dto.NetWork,
                [Keys.NetworkTo] = dto.ToChainId,
                [Keys.AmountFrom] = dto.FromAmount.ToString(),
                [Keys.ChainIdTo] = dto.ToChainId,
                [Keys.Reason] = dto.Reason,
                [Keys.CreateTime] = dto.CreateTime != null ? DateTimeHelper.FromUnixTimeMilliseconds(dto.CreateTime.Value).ToUtcString(TimeHelper.UtcPattern) : DateTime.UtcNow.ToUtcString(TimeHelper.UtcPattern)
            }
        });
    }

    private static class Keys
    {
        public const string UserId = "userId";
        public const string OrderType = "orderType";
        public const string OrderId = "orderId";
        public const string CreateTime = "createTime";
        public const string TxId = "txId";
        public const string Reason = "reason";
        public const string AmountFrom = "amountFrom";

        //from
        public const string NetworkFrom = "networkFrom";
        public const string SymbolFrom = "symbolFrom";

        //to
        public const string NetworkTo = "networkTo";
        public const string ChainIdTo = "chainIdTo";
        public const string SymbolTo = "symbolTo";
    }
}