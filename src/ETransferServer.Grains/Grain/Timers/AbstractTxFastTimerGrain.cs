using AElf.Client.Dto;
using ETransfer.Contracts.TokenPool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Google.Protobuf;
using NBitcoin;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface IBaseTxFastTimerGrain : IGrainWithGuidKey
{
    public Task AddToPendingList(Guid id, TimerTransaction transaction);

    public Task<DateTime> GetLastCallBackTime();
}

public abstract class AbstractTxFastTimerGrain<TOrder> : Grain<OrderTimerState> where TOrder : BaseOrderDto
{
    internal DateTime LastCallBackTime;

    private readonly ILogger<AbstractTxFastTimerGrain<TOrder>> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ITokenTransferProvider _transferProvider;
    private readonly IUserWithdrawProvider _userWithdrawProvider;

    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;

    protected AbstractTxFastTimerGrain(ILogger<AbstractTxFastTimerGrain<TOrder>> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, IOptionsSnapshot<TimerOptions> timerOptions, 
        IOptionsSnapshot<WithdrawOptions> withdrawOptions, ITokenTransferProvider transferProvider,
        IUserWithdrawProvider userWithdrawProvider)
    {
        _logger = logger;
        _contractProvider = contractProvider;
        _chainOptions = chainOptions;
        _timerOptions = timerOptions;
        _withdrawOptions = withdrawOptions;
        _transferProvider = transferProvider;
        _userWithdrawProvider = userWithdrawProvider;
    }


    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(LastCallBackTime);
    }

    public async Task AddToPendingList(Guid id, TimerTransaction transaction)
    {
        if (State.OrderTransactionDict.ContainsKey(id))
        {
            _logger.LogWarning("Order id {Id} exists in OrderTxFastTimerGrain state", id);
            return;
        }

        AssertHelper.NotNull(transaction, "Transaction empty");
        AssertHelper.NotEmpty(transaction.ChainId, "Transaction chainId empty");
        AssertHelper.NotEmpty(transaction.TxId, "Transaction id empty");
        AssertHelper.NotNull(transaction.TxTime, "Transaction time null");

        State.OrderTransactionDict[id] = transaction;

        await WriteStateAsync();
    }

    internal async Task TimerCallback(object state)
    {
        var total = State.OrderTransactionDict.Count;
        _logger.LogDebug("OrderTxFastTimerGrain callback, Total={Total}", total);
        LastCallBackTime = DateTime.UtcNow;
        if (total < 1)
        {
            return;
        }

        // chainIds of pending list
        var chainIds = State.OrderTransactionDict.Values
            .Select(tx => tx.ChainId)
            .Distinct().ToList();
        
        var chainStatus = new Dictionary<string, ChainStatusDto>();
        foreach (var chainId in chainIds)
        {
            chainStatus[chainId] = await _contractProvider.GetChainStatusAsync(chainId);
            if (chainStatus[chainId] != null)
                _logger.LogDebug("TxFastTimer node chainId={ChainId}, Height= {Height}", chainId,
                    chainStatus[chainId].LongestChainHeight);
        }
        
        var removed = 0;
        const int subPageSize = 10;
        var pendingList = State.OrderTransactionDict.ToList();
        var pendingSubLists = SplitList(pendingList, subPageSize);
        foreach (var subList in pendingSubLists)
        {
            var txIds = subList.Select(t => t.Value.TxId).Distinct().ToList();
            var pager = await _transferProvider.GetTokenTransferInfoByTxIdsAsync(txIds, 0);
            var indexerTxDict = pager.Items.ToDictionary(t => t.TransactionId, t => t);
            var handleResult = await HandlePage(subList, chainStatus, indexerTxDict);
            foreach (var (orderId, remove) in handleResult)
            {
                if (!remove) continue;
                removed++;
                _logger.LogDebug("OrderTxFastTimerGrain remove {RemovedOrderId}", orderId);
                State.OrderTransactionDict.Remove(orderId);
            }

            await WriteStateAsync();
        }

        _logger.LogInformation("OrderTxFastTimerGrain finish, count: {Removed}/{Total}", removed, total);
    }

    private IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 10)
    {
        for (var i = 0; i < locations.Count; i += nSize)
        {
            yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
        }
    }
    
    internal abstract Task SaveOrder(TOrder order);
    
    internal abstract Task SaveOrder(TOrder order, Dictionary<string, string> externalInfo);

    internal abstract Task<TOrder> GetOrder(Guid orderId);


    private async Task<Dictionary<Guid, bool>> HandlePage(List<KeyValuePair<Guid, TimerTransaction>> pendingList, 
        Dictionary<string, ChainStatusDto> chainStatusDict, Dictionary<string, TransferDto> indexerTx)
    {
        _logger.LogDebug("TxFastTimer handle page, pendingCount={Count}", pendingList.Count);
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var result = new Dictionary<Guid, bool>();
        foreach (var (orderId, pendingTx) in pendingList)
        {
            // query order and verify pendingTx data
            var order = await QueryOrderAndVerify(orderId, pendingTx);
            if (order == null)
            {
                result[orderId] = true;
                continue;
            }

            // The following two cases directly from the node query results
            // The order has been in the list for a long time.
            var queryNode = now > pendingTx.TxTime + _chainOptions.Value.TxResultFastFromNodeSecondsAfter * 1000;
            if (queryNode)
            {
                _logger.LogDebug("TxFastTimer use node result orderId={OrderId}, chainId={ChainId}, txId={TxId}", orderId,
                    pendingTx.ChainId, pendingTx.TxId);
                var status = await HandleOrderTransaction(order, pendingTx, chainStatusDict[pendingTx.ChainId]);
                await AddToPendingList(order, pendingTx, status);
                result[orderId] = true;
                continue;
            }

            _logger.LogDebug("TxFastTimer use indexer result orderId={OrderId}, chainId={ChainId}, txId={TxId}, " +
                "indexerTxCount={Count}", orderId, pendingTx.ChainId, pendingTx.TxId, indexerTx.Count);
            if (!indexerTx.ContainsKey(pendingTx.TxId))
            {
                result[orderId] = false;
                continue;
            }
            
            //fast confirmed
            if (!IsTxFastConfirmed(pendingTx, order, indexerTx, chainStatusDict[pendingTx.ChainId]))
            {
                result[orderId] = false;
                if (pendingTx.TransferType == TransferTypeEnum.FromTransfer.ToString())
                {
                    await SaveOrder(order);
                }
                continue;
            }

            // Transfer data from indexer
            _logger.LogDebug("TxFastTimer indexer transaction exists, orderId={OrderId}, txId={TxId}", orderId,
                pendingTx.TxId);
            var transfer = indexerTx[pendingTx.TxId];
            if (pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString())
            {
                order.ToTransfer.Status = OrderTransferStatusEnum.Transferred.ToString();
                order.Status = OrderStatusEnum.ToTransferConfirmed.ToString();
                await ChangeOperationStatus(order);
            }
            else
            {
                order.FromTransfer.FromAddress = transfer.FromAddress;
                order.FromTransfer.ToAddress = transfer.ToAddress;
                order.FromTransfer.Status = OrderTransferStatusEnum.Transferred.ToString();
                order.Status = OrderStatusEnum.FromTransferConfirmed.ToString();
            }
            await SaveOrder(order, ExtensionBuilder.New()
                .Add(ExtensionKey.TransactionStatus, transfer.Status)
                .Build());
            await AddToPendingList(order, pendingTx, pendingTx.TransferType == TransferTypeEnum.FromTransfer.ToString());
            result[orderId] = true;
        }

        return result;
    }

    private async Task AddToPendingList(TOrder order, TimerTransaction pendingTx, bool status)
    {
        try
        {
            var withdrawTimerGrain =
                GrainFactory.GetGrain<IUserWithdrawTxTimerGrain>(
                    GuidHelper.UniqGuid(nameof(IUserWithdrawTxTimerGrain)));
            var isToTransfer = pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString();
            var transferInfo = isToTransfer ? order.ToTransfer : order.FromTransfer;
            await withdrawTimerGrain.AddToPendingList(order.Id, new TimerTransaction
            {
                TxId = transferInfo.TxId,
                TxTime = transferInfo.TxTime,
                ChainId = transferInfo.ChainId,
                TransferType = pendingTx.TransferType,
                IsForward = !status
            });
        }
        catch (Exception e)
        {
            _logger.LogError("TxFastTimer error, orderId={OrderId} Message={Msg}", order.Id, e.Message);
        }
    }

    /// <summary>
    ///     Query the transaction result and return whether the processing is completed.
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="timerTx"></param>
    /// <param name="chainStatusDict"></param>
    /// <returns>
    ///     true: Processing complete, need to remove from list
    ///     false:Transaction to be confirmed, continue to wait
    /// </returns>
    internal async Task<bool> HandleOrderTransaction(TOrder order, TimerTransaction timerTx, ChainStatusDto chainStatus)
    {
        var txDateTime = TimeHelper.GetDateTimeFromTimeStamp(timerTx.TxTime ?? 0);
        var txExpireTime = txDateTime.AddSeconds(_chainOptions.Value.Contract.TransactionTimerMaxSeconds);

        TransferInfo transferInfo;
        bool isToTransfer;
        try
        {
            /*
             * Check the abnormal data, these exceptions should not occur,
             * if so, these data should be kicked out of the list and discarded.
             */
            isToTransfer = timerTx.TransferType == TransferTypeEnum.ToTransfer.ToString();
            transferInfo = isToTransfer ? order.ToTransfer : order.FromTransfer;
            AssertHelper.NotNull(transferInfo, "Order transfer not found, txId={TxId}", timerTx.TxId);
            AssertHelper.IsTrue(transferInfo.TxId == timerTx.TxId,
                "Timer txId not match, expected={TimerTxId}, actually={TxId}", timerTx.TxId, transferInfo.TxId);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("Order timer error, orderId={OrderId} Message={Msg}", order.Id, e.Message);
            return true;
        }

        try
        {
            // When the transaction is just sent to the node,
            // the query may appear NotExisted status immediately, so this is to skip this period of time
            if (transferInfo.TxTime > DateTime.UtcNow.AddSeconds(2).ToUtcMilliSeconds())
            {
                return false;
            }

            var txStatus =
                await _contractProvider.QueryTransactionResultAsync(transferInfo.ChainId, transferInfo.TxId);
            _logger.LogInformation(
                "TxFastOrderTimer order={OrderId}, txId={TxId}, status={Status}, bestHeight={BestHeight}, txHeight={Height}, LIB={Lib}", 
                order.Id, timerTx.TxId, txStatus.Status, chainStatus.BestChainHeight, txStatus.BlockNumber, 
                chainStatus.LastIrreversibleBlockHeight);

            if (!isToTransfer)
            {
                order.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum, (chainStatus.BestChainHeight - txStatus.BlockNumber).ToString());
            }

            // pending status, continue waiting
            if (txStatus.Status == CommonConstant.TransactionState.Pending) return false;

            // Transaction is packaged and requires multiple verification
            if (txStatus.Status == CommonConstant.TransactionState.Mined)
            {
                if (!IsTxFastConfirmed(timerTx, order, txStatus.BlockNumber, chainStatus)) return false;

                if (!isToTransfer)
                {
                    var (from, to) = await ParseFromLogEvent(txStatus.Logs);
                    if (!from.IsNullOrEmpty())
                    {
                        transferInfo.FromAddress = from;
                        transferInfo.ToAddress = to;
                    }
                }
                else
                {
                    await ChangeOperationStatus(order);
                }

                transferInfo.Status = OrderTransferStatusEnum.Transferred.ToString();
                order.Status = isToTransfer
                    ? OrderStatusEnum.ToTransferConfirmed.ToString()
                    : OrderStatusEnum.FromTransferConfirmed.ToString();

                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                return true;
            }

            if (txStatus.Status == CommonConstant.TransactionState.Failed
                || txStatus.Status == CommonConstant.TransactionState.NodeValidationFailed
                || txStatus.Status == CommonConstant.TransactionState.NotExisted)
            {
                transferInfo.Status = OrderTransferStatusEnum.Failed.ToString();
                order.Status = isToTransfer
                    ? OrderStatusEnum.ToTransferFailed.ToString()
                    : OrderStatusEnum.FromTransferFailed.ToString();
                if (isToTransfer) await ChangeOperationStatus(order, false);
                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                _logger.LogWarning(
                    "TxFastOrderTimer tx status {TxStatus}, orderId={OrderId}. txId={TxId}, error={Error}", 
                    txStatus.Status, order.Id, transferInfo.TxId, txStatus.Error);
                return true;
            }

            _logger.LogWarning("Unknown txStatus txId={TxId} status={Status}, orderId={OrderId} ,continue wait..",
                txStatus.TransactionId, transferInfo.Status, order.Id);
            return false;
        }
        catch (Exception e)
        {
            if (txExpireTime < DateTime.UtcNow)
            {
                _logger.LogError(e, "TxFastOrderTimer timer tx expired, orderId={OrderId}, txId={TxId}, txTime={Time}",
                    order.Id,
                    timerTx.TxTime, timerTx.TxTime);
                return true;
            }

            _logger.LogError(e, "TxFastOrderTimer timer handle tx failed, orderId={OrderId}, txId={TxId}, txTime={Time}",
                order.Id,
                timerTx.TxTime, timerTx.TxTime);
            return false;
        }
    }

    private async Task<Tuple<string, string>> ParseFromLogEvent(LogEventDto[] logs)
    {
        try
        {
            var param = TokenPoolTransferred.Parser.ParseFrom(
                ByteString.FromBase64(logs.FirstOrDefault(l => l.Name == nameof(TokenPoolTransferred))?.NonIndexed));
            _logger.LogInformation(
                "TxFastOrderTimer ParseFromLogEvent, from={from}, to={to}", param.From.ToBase58(), param.To.ToBase58());
            return Tuple.Create(param.From.ToBase58(), param.To.ToBase58());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TxFastOrderTimer ParseFromLogEvent error");
            return Tuple.Create(string.Empty, string.Empty);
        }
    }

    private async Task<TOrder> QueryOrderAndVerify(Guid orderId, TimerTransaction timerTx)
    {
        TOrder order;
        try
        {
            /*
             * Check the abnormal data, these exceptions should not occur,
             * if so, these data should be kicked out of the list and discarded.
             */
            order = await GetOrder(orderId);
            AssertHelper.NotNull(order, "TxFastTimer Query order empty");
            return order;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("TxFastTimer order timer error, OrderId={OrderId} Message={Msg}", orderId, e.Message);
            return null;
        }
    }
    
    private async Task ChangeOperationStatus(TOrder order, bool success = true)
    {
        order.ExtensionInfo ??= new Dictionary<string, string>();
        if (!order.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)) return;
        
        if (order.ExtensionInfo[ExtensionKey.SubStatus] == OrderOperationStatusEnum.ReleaseConfirming.ToString())
        {
            order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, success
                ? OrderOperationStatusEnum.ReleaseConfirmed.ToString()
                : OrderOperationStatusEnum.ReleaseFailed.ToString());
        }
        else if (order.ExtensionInfo[ExtensionKey.SubStatus] == OrderOperationStatusEnum.RefundConfirming.ToString())
        {
            order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, success
                ? OrderOperationStatusEnum.RefundConfirmed.ToString()
                : OrderOperationStatusEnum.RefundFailed.ToString());
        }
    }

    private bool IsTxFastConfirmed(TimerTransaction pendingTx, TOrder order,
        Dictionary<string, TransferDto> indexerTx, ChainStatusDto chainStatus)
    {
        try
        {
            (var amount, var symbol, var amountThreshold, var blockHeightUpperThreshold,
                    var blockHeightLowerThreshold) = GetTxFastTransactionData(pendingTx, order);

            var indexerHeight = chainStatus.BestChainHeight;
            var txBlockHeight = indexerTx[pendingTx.TxId].BlockHeight;
            if (pendingTx.TransferType == TransferTypeEnum.FromTransfer.ToString())
            {
                order.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum, (indexerHeight - txBlockHeight).ToString());
            }

            _logger.LogDebug(
                "TxFastTimer IsTxFastConfirmed: amount={amount}, symbol={symbol}, amountThreshold={amountThreshold}, " +
                "blockHeightUpperThreshold={blockHeightUpperThreshold}, blockHeightLowerThreshold={blockHeightLowerThreshold}, " +
                "indexerHeight={indexerHeight}, txBlockHeight={txBlockHeight}", amount, symbol, amountThreshold,
                blockHeightUpperThreshold, blockHeightLowerThreshold, indexerHeight, txBlockHeight);

            return (amount > amountThreshold && indexerHeight - txBlockHeight >= blockHeightUpperThreshold)
                   || (amount <= amountThreshold && indexerHeight - txBlockHeight >= blockHeightLowerThreshold);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("TxFastTimer IsTxFastConfirmed error, OrderId={OrderId} Message={Msg}", 
                order.Id, e.Message);
            return false;
        }
    }

    private bool IsTxFastConfirmed(TimerTransaction pendingTx, TOrder order, long txBlockNumber, ChainStatusDto chainStatus)
    {
        try
        {
            (var amount, var symbol, var amountThreshold, var blockHeightUpperThreshold,
                var blockHeightLowerThreshold) = GetTxFastTransactionData(pendingTx, order);

            var indexerHeight = chainStatus.BestChainHeight;

            _logger.LogDebug(
                "TxFastTimer IsTxFastConfirmed: amount={amount}, symbol={symbol}, amountThreshold={amountThreshold}, " +
                "blockHeightUpperThreshold={blockHeightUpperThreshold}, blockHeightLowerThreshold={blockHeightLowerThreshold}, " +
                "indexerHeight={indexerHeight}, txBlockNumber={txBlockNumber}", amount, symbol, amountThreshold,
                blockHeightUpperThreshold, blockHeightLowerThreshold, indexerHeight, txBlockNumber);

            return (amount > amountThreshold && indexerHeight - txBlockNumber >= blockHeightUpperThreshold)
                   || (amount <= amountThreshold && indexerHeight - txBlockNumber >= blockHeightLowerThreshold);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("TxFastTimer IsTxFastConfirmed Chain error, OrderId={OrderId} Message={Msg}", 
                order.Id, e.Message);
            return false;
        }
    }

    private (decimal, string, long, long, long) GetTxFastTransactionData(TimerTransaction pendingTx, TOrder order)
    {
        var amount = pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString()
            ? order.ToTransfer.Amount
            : order.FromTransfer.Amount;

        var symbol = pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString()
            ? order.ToTransfer.Symbol
            : order.FromTransfer.Symbol;
        var thresholdExists = _withdrawOptions.Value.Homogeneous.TryGetValue(symbol, out var threshold);
        AssertHelper.IsTrue(thresholdExists, "Homogeneous symbol {} not found", symbol);
        AssertHelper.NotNull(threshold, "Homogeneous threshold not fount, symbol:{}", symbol);

        var amountThreshold = threshold.AmountThreshold;
        var blockHeightUpperThreshold = threshold.BlockHeightUpperThreshold;
        var blockHeightLowerThreshold = threshold.BlockHeightLowerThreshold;

        return (amount, symbol, amountThreshold, blockHeightUpperThreshold, blockHeightLowerThreshold);
    }
}