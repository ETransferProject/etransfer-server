using CAServer.Commons;
using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Orleans.Streams;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IWithdrawFeeMonitorGrain : IGrainWithStringKey
{
    public static string GrainId(ThirdPartServiceNameEnum serviceName, string network, string symbol)
    {
        return string.Join(CommonConstant.Underline, serviceName, network, symbol);
    }

    public Task DoMonitor(FeeInfo feeInfo);
}

public class WithdrawFeeMonitorGrain : Grain<WithdrawFeeMonitorDto>, IWithdrawFeeMonitorGrain
{
    private const string AlarmNotifyTemplate = "WithdrawFeeAlarm";

    private readonly ILogger<WithdrawFeeMonitorGrain> _logger;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;
    private readonly IOptionsMonitor<WithdrawNetworkOptions> _withdrawNetworkOptions;


    public WithdrawFeeMonitorGrain(ILogger<WithdrawFeeMonitorGrain> logger,
        IOptionsMonitor<WithdrawNetworkOptions> withdrawNetworkOptions, IEnumerable<INotifyProvider> notifyProvider)
    {
        _logger = logger;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }

    public async Task DoMonitor(FeeInfo feeInfo)
    {
        try
        {
            var idVals = this.GetPrimaryKeyString().Split(CommonConstant.Underline);
            AssertHelper.IsTrue(idVals.Length >= 3, "Invalid GrainId {}", this.GetPrimaryKeyString());
            var serviceName = idVals[0];
            var network = idVals[1];
            var symbol = idVals[2];

            var currentFee = feeInfo;
            AssertHelper.NotNull(currentFee, "ThirdPartFee not found");
            AssertHelper.IsTrue(currentFee.Amount.SafeToDecimal() > 0, "Invalid thirdPartFee");

            var netWorkInfo =
                _withdrawNetworkOptions.CurrentValue.NetworkInfos.FirstOrDefault(n =>
                    n.Coin == string.Join(CommonConstant.Underline, network, symbol));
            AssertHelper.NotNull(netWorkInfo, "Network {} not found", network);

            var latestFeeTime = State.FeeTime;
            var latestFee = State.FeeInfos.FirstOrDefault();

            State.FeeInfos = new List<FeeInfo> { feeInfo };
            State.FeeTime = DateTime.UtcNow;
            await WriteStateAsync();

            if (latestFee == null) return;

            var currentFeeAmount = currentFee.Amount.SafeToDecimal();
            var latestFeeAmount = latestFee.Amount.SafeToDecimal();
            var percent = Math.Abs(currentFeeAmount - latestFeeAmount) / latestFeeAmount * 100;
            if (percent <= netWorkInfo.FeeAlarmPercent) return;

            var sendSuccess =
                await SendNotifyAsync(network, feeInfo.Symbol, currentFee, latestFee, latestFeeTime, percent);
            AssertHelper.IsTrue(sendSuccess, "Send notify failed");
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "Withdraw fee monitor handle failed , Message={Msg}, GrainId={GrainId} feeInfo={FeeInfo}", e.Message,
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(feeInfo));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Withdraw fee monitor handle failed GrainId={GrainId}, feeInfo={FeeInfo}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(feeInfo));
        }
    }


    private async Task<bool> SendNotifyAsync(string network, string symbol, FeeInfo currentFee, FeeInfo latestFee,
        DateTime latestFeeTime, decimal percent)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = AlarmNotifyTemplate,
            Params = new Dictionary<string, string>
            {
                [Keys.Network] = network,
                [Keys.Percent] = percent.ToString(2),
                [Keys.LastFeeTime] = latestFeeTime.ToUtcString(TimeHelper.UtcPattern),
                [Keys.LastFeeAmount] = latestFee.Amount,
                [Keys.LastFeeSymbol] = symbol,
                [Keys.CurrentFeeTime] = DateTime.UtcNow.ToUtcString(TimeHelper.UtcPattern),
                [Keys.CurrentFeeAmount] = currentFee.Amount,
                [Keys.CurrentFeeSymbol] = symbol
            }
        });
    }

    private static class Keys
    {
        public const string Network = "network";
        public const string Percent = "percent";
        public const string LastFeeAmount = "lastFeeAmount";
        public const string LastFeeSymbol = "lastFeeSymbol";
        public const string LastFeeTime = "lastFeeTime";
        public const string CurrentFeeAmount = "currentFeeAmount";
        public const string CurrentFeeSymbol = "currentFeeSymbol";
        public const string CurrentFeeTime = "currentFeeTime";
    }
}