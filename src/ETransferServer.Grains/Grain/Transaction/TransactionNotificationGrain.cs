using ETransferServer.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Worker.Transaction;

[StatelessWorker]
public class TransactionNotificationGrain : Orleans.Grain, ITransactionNotificationGrain
{
    private readonly ILogger<TransactionNotificationGrain> _logger;
    private readonly IOptionsSnapshot<NetworkOptions> _networkOption;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOption;
    private readonly ICoBoProvider _coBoProvider;
    private IDepositOrderStatusReminderGrain _depositOrderStatusReminderGrain;

    public TransactionNotificationGrain(ILogger<TransactionNotificationGrain> logger,
        IOptionsSnapshot<NetworkOptions> networkOption,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOption, 
        ICoBoProvider coBoProvider)
    {
        _logger = logger;
        _networkOption = networkOption;
        _depositAddressOption = depositAddressOption;
        _coBoProvider = coBoProvider;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _depositOrderStatusReminderGrain =
            GrainFactory.GetGrain<IDepositOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderStatusReminderGrain)));
        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<bool> TransactionNotification(string timestamp, string signature, string body)
    {
        try
        {
            return await HandleTransaction(body);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "transaction notification error, timestamp:{timestamp}, signature:{signature}, body:{body}", timestamp,
                signature, body);
            return false;
        }
    }

    private async Task<bool> HandleTransaction(string body)
    {
        var coBoTransaction = JsonConvert.DeserializeObject<CoBoTransactionDto>(body);
        coBoTransaction.Coin = _coBoProvider.GetResponseCoin(coBoTransaction.Coin);
        AssertHelper.NotNull(coBoTransaction, "DeserializeObject to CoBoTransactionDto fail, invalid body: {body}",
            body);
        if (coBoTransaction.AbsAmount.SafeToDecimal() <= 0M)
        {
            _logger.LogInformation("transaction callback amount invalid");
            return true;
        }

        var handleResult = false;
        switch (coBoTransaction.Side)
        {
            case CommonConstant.Deposit:
                handleResult = await HandleDeposit(coBoTransaction);
                break;
            case CommonConstant.Withdraw:
                handleResult = await HandleWithdraw(coBoTransaction);
                break;
            default:
                throw new UserFriendlyException($"invalid transaction type:{coBoTransaction.Side}");
        }

        return handleResult;
    }

    private async Task<bool> HandleDeposit(CoBoTransactionDto coBoTransaction)
    {
        await GetCoinNetwork(coBoTransaction);
        var coBoDepositQueryTimerGrain = GrainFactory.GetGrain<ICoBoDepositQueryTimerGrain>(
            GuidHelper.UniqGuid(nameof(ICoBoDepositQueryTimerGrain)));
        if (coBoTransaction.Status == CommonConstant.PendingStatus)
        {
            await coBoDepositQueryTimerGrain.SaveDepositRecord(coBoTransaction);
        }

        if (coBoTransaction.Status == CommonConstant.SuccessStatus)
        {
            var verifyResult = await VerifyTransaction(coBoTransaction);
            AssertHelper.IsTrue(verifyResult, "transaction verify fail.");

            // create order
            await coBoDepositQueryTimerGrain.CreateDepositRecord(coBoTransaction);
        }
        return true;
    }

    private Task<bool> HandleWithdraw(CoBoTransactionDto coBoTransaction)
    {
        return Task.FromResult(true);
    }

    private async Task<CoBoHelper.CoinNetwork> GetCoinNetwork(CoBoTransactionDto coBoTransaction)
    {
        try
        {
            return CoBoHelper.MatchNetwork(coBoTransaction.Coin, _networkOption.Value.CoBo);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "match network error, id:{id}, coin:{coin}", coBoTransaction.Id, coBoTransaction.Coin);
            if (_depositAddressOption.Value.AddressWhiteLists.Contains(coBoTransaction.Address))
            {
                _logger.LogInformation("deposit callback hit address whiteList: {address}", coBoTransaction.Address);
                throw;
            }
            var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(coBoTransaction.Id);
            if (await coBoDepositGrain.NeedUpdate())
            {
                _logger.LogInformation("coBoDepositGrain update, Id: {id}, CombinedId: {combineId}", coBoTransaction.Id, GuidHelper.GenerateCombinedId(coBoTransaction.Id,
                    CommonConstant.DepositOrderCoinNotSupportAlarm));
                await coBoDepositGrain.AddOrUpdate(coBoTransaction);
                if (await coBoDepositGrain.NotUpdated())
                {
                    await _depositOrderStatusReminderGrain.AddReminder(GuidHelper.GenerateCombinedId(coBoTransaction.Id,
                        CommonConstant.DepositOrderCoinNotSupportAlarm));
                }
            }

            throw;
        }
    }

    private async Task<bool> VerifyTransaction(CoBoTransactionDto coBoTransaction)
    {
        var transactionDto = await _coBoProvider.GetTransactionAsync(coBoTransaction.Id);
        AssertHelper.NotNull(transactionDto, "get transaction is null.");
        AssertHelper.IsTrue(transactionDto.Status == CommonConstant.SuccessStatus,
            "transaction status is not success.");

        if (transactionDto.Id == coBoTransaction.Id &&
            transactionDto.Status == coBoTransaction.Status &&
            transactionDto.Coin == coBoTransaction.Coin &&
            transactionDto.Decimal == coBoTransaction.Decimal &&
            transactionDto.Address == coBoTransaction.Address &&
            transactionDto.Amount == coBoTransaction.Amount)
        {
            return true;
        }

        _logger.LogWarning(
            "transaction verify fail, transactionDto:{transactionDto}, coBoTransaction:{coBoTransaction}",
            JsonConvert.SerializeObject(transactionDto), JsonConvert.SerializeObject(coBoTransaction));
        return false;
    }
}