using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface IDepositSwapMonitorGrain : IGrainWithStringKey
{
    Task DoMonitor(DepositSwapMonitorDto depositSwapMonitorDto);
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
    
    public async Task DoMonitor(DepositSwapMonitorDto dto)
    {
        try
        {
            var sendSuccess =
                await SendNotifyAsync(dto);
            AssertHelper.IsTrue(sendSuccess, "Send notify failed");
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "Deposit swap monitor handle failed , Message={Msg}, GrainId={GrainId} dto={dto}", e.Message,
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Deposit swap monitor handle failed GrainId={GrainId}, feeInfo={dto}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
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