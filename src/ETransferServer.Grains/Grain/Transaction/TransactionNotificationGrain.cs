using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Order;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User;
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
    private readonly IUserAddressService _userAddressService;
    private readonly IOrderAppService _orderAppService;
    private readonly IBlockchainClientProviderFactory _blockchainClientProvider;
    private readonly ICoBoProvider _coBoProvider;
    private IDepositOrderStatusReminderGrain _depositOrderStatusReminderGrain;

    public TransactionNotificationGrain(ILogger<TransactionNotificationGrain> logger,
        IOptionsSnapshot<NetworkOptions> networkOption,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOption, 
        IUserAddressService userAddressService,
        IOrderAppService orderAppService,
        IBlockchainClientProviderFactory blockchainClientProvider,
        ICoBoProvider coBoProvider)
    {
        _logger = logger;
        _networkOption = networkOption;
        _depositAddressOption = depositAddressOption;
        _userAddressService = userAddressService;
        _orderAppService = orderAppService;
        _blockchainClientProvider = blockchainClientProvider;
        _coBoProvider = coBoProvider;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _depositOrderStatusReminderGrain =
            GrainFactory.GetGrain<IDepositOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderStatusReminderGrain)));
        await base.OnActivateAsync(cancellationToken);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TransactionNotificationGrain), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<bool> TransactionNotification(string timestamp, string signature, string body)
    {
        return await HandleTransaction(body);
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, string timestamp, string signature, string body)
    {
        _logger.LogError(ex,
            "transaction notification error, timestamp:{timestamp}, signature:{signature}, body:{body}", timestamp,
            signature, body);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
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
        var coinInfo = await GetCoinNetwork(coBoTransaction);
        
        // transfer
        var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(coBoTransaction.Address);
        var res = await addressGrain.Get();
        var userAddress = !res.Success || res.Data == null
            ? await _userAddressService.GetAssignedAddressAsync(coBoTransaction.Address)
            : res.Value;

        AssertHelper.NotNull(userAddress, "user address empty.");
        if (userAddress.IsAssigned && !userAddress.OrderId.IsNullOrEmpty())
        {
            _logger.LogInformation("transfer callback start. {id}", coBoTransaction.Id);
            var orderId = !_depositAddressOption.Value.TransferAddressLists.IsNullOrEmpty() &&
                          _depositAddressOption.Value.TransferAddressLists.ContainsKey(coBoTransaction.Coin) &&
                          _depositAddressOption.Value.TransferAddressLists[coBoTransaction.Coin]
                              .Contains(coBoTransaction.Address)
                ? await GetTransferOrderIdAsync(coBoTransaction)
                : userAddress.OrderId;

            AssertHelper.IsTrue(Guid.TryParse(orderId, out _), "transfer orderId invalid.");
            var withdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(Guid.Parse(orderId));
            await withdrawGrain.SaveTransferOrder(coBoTransaction);
            return true;
        }
        if (!userAddress.IsAssigned && userAddress.OrderId == string.Empty)
        {
            _logger.LogInformation("transfer callback but address recycled. {id}", coBoTransaction.Id);
            return true;
        }
        if (userAddress.IsAssigned && userAddress.OrderId == string.Empty)
        {
            _logger.LogInformation("deposit callback check. {id}", coBoTransaction.Id);
            var id = OrderIdHelper.DepositOrderId(coinInfo.Network, coinInfo.Symbol, coBoTransaction.TxId);
            var depositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(id);
            var order = (await depositRecordGrain.GetAsync())?.Value;
            if (order == null)
            {
                if (await _orderAppService.CheckTransferOrderAsync(coBoTransaction, userAddress.UpdateTime))
                {
                    _logger.LogInformation("deposit callback hit transfer order. {id}", coBoTransaction.Id);
                    return true;
                }
            }
        }

        // deposit
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

    private async Task<string> GetTransferOrderIdAsync(CoBoTransactionDto coBoTransaction, int retry = 0)
    {
        var memo = string.Empty;
        if (retry > _depositAddressOption.Value.MaxRequestRetryTimes)
        {
            _logger.LogError("Get memo failed after retry {maxRetry}, {coin}.",
                _depositAddressOption.Value.MaxRequestRetryTimes, coBoTransaction.Coin);
            return memo;
        }
        try
        {
            var coin = coBoTransaction.Coin.Split(CommonConstant.Underline);
            var provider = await _blockchainClientProvider.GetBlockChainClientProviderAsync(coin[0]);
            switch (provider.ChainType)
            {
                case BlockchainType.Ton:
                    memo = await provider.GetMemoAsync(coin[0], coBoTransaction.TxId);
                    AssertHelper.IsTrue(!memo.IsNullOrEmpty(), "get memo empty.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get memo: {coin}.", coBoTransaction.Coin);
            retry += 1;
            await GetTransferOrderIdAsync(coBoTransaction, retry);
        }

        return memo;
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