using ETransferServer.Common;
using ETransferServer.Grains.Common;
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
    private readonly IOptionsMonitor<NetworkOptions> _networkOption;
    private readonly ICoBoProvider _coBoProvider;

    public TransactionNotificationGrain(ILogger<TransactionNotificationGrain> logger,
        IOptionsMonitor<NetworkOptions> networkOption, ICoBoProvider coBoProvider)
    {
        _logger = logger;
        _networkOption = networkOption;
        _coBoProvider = coBoProvider;
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
        AssertHelper.NotNull(coBoTransaction, "DeserializeObject to CoBoTransactionDto fail, invalid body: {body}",
            body);

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
        var coinInfo = CoBoHelper.MatchNetwork(coBoTransaction.Coin, _networkOption.CurrentValue.CoBo);
        var recordGrainId = OrderIdHelper.DepositOrderId(coinInfo.Network, coinInfo.Symbol, coBoTransaction.TxId);
        var userDepositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(recordGrainId);

        var orderDto = await userDepositRecordGrain.GetAsync();
        if (orderDto.Value != null)
        {
            // order already exist.
            _logger.LogWarning("order already exist, orderId: {orderId}", orderDto.Value.Id);
            return true;
        }

        var verifyResult = await VerifyTransaction(coBoTransaction);
        AssertHelper.IsTrue(verifyResult, "transaction verify fail.");

        var coBoDepositQueryTimerGrain = GrainFactory.GetGrain<ICoBoDepositQueryTimerGrain>(
            GuidHelper.UniqGuid(nameof(ICoBoDepositQueryTimerGrain)));
        // create order
        await coBoDepositQueryTimerGrain.CreateDepositRecord(coBoTransaction);
        return true;
    }

    private Task<bool> HandleWithdraw(CoBoTransactionDto coBoTransaction)
    {
        return Task.FromResult(true);
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