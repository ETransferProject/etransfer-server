using AElf.Client.Dto;
using ETransfer.Contracts.TokenPool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Google.Protobuf;
using NBitcoin;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface IBaseTxTimerGrain : IGrainWithGuidKey
{
    public Task AddToPendingList(Guid id, TimerTransaction transaction);

    public Task<DateTime> GetLastCallBackTime();
}

public abstract class AbstractTxTimerGrain<TOrder, TTimeState> : Grain<TTimeState> 
    where TOrder : BaseOrderDto 
    where TTimeState : OrderTimerState
{
    internal DateTime LastCallBackTime;

    private readonly ILogger<AbstractTxTimerGrain<TOrder, TTimeState>> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ITokenTransferProvider _transferProvider;
    private readonly IUserWithdrawProvider _userWithdrawProvider;
    private readonly IUserDepositProvider _userDepositProvider;

    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    protected AbstractTxTimerGrain(ILogger<AbstractTxTimerGrain<TOrder, TTimeState>> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, IOptionsSnapshot<TimerOptions> timerOptions,
        ITokenTransferProvider transferProvider, IUserWithdrawProvider userWithdrawProvider,
        IUserDepositProvider userDepositProvider)
    {
        _logger = logger;
        _contractProvider = contractProvider;
        _chainOptions = chainOptions;
        _timerOptions = timerOptions;
        _transferProvider = transferProvider;
        _userWithdrawProvider = userWithdrawProvider;
        _userDepositProvider = userDepositProvider;
    }


    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(LastCallBackTime);
    }

    public async Task AddToPendingList(Guid id, TimerTransaction transaction)
    {
        if (State.OrderTransactionDict.ContainsKey(id))
        {
            _logger.LogWarning("Order id {Id} exists in OrderTxTimerGrain state", id);
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
        _logger.LogDebug("OrderTxTimerGrain callback, Total={Total}", total);
        _logger.LogInformation("OrderTxTimerGrain grainId: {GrainId},{Key},{Count}", 
            this.GetGrainId(), this.GetPrimaryKey(), total);
        LastCallBackTime = DateTime.UtcNow;
        if (total < 1)
        {
            return;
        }

        // chainIds of pending list
        var chainIds = State.OrderTransactionDict.Values
            .Select(tx => tx.ChainId)
            .Distinct().ToList();

        var removed = 0;
        var indexerLatestHeight = new Dictionary<string, long>();
        var chainStatus = new Dictionary<string, ChainStatusDto>();
        foreach (var chainId in chainIds)
        {
            chainStatus[chainId] = await _contractProvider.GetChainStatusAsync(chainId);
            if (chainStatus[chainId] != null)
                _logger.LogDebug("TxTimer node chainId={ChainId}, Height= {Height}, LibHeight= {libHeight}", 
                    chainId, chainStatus[chainId].LongestChainHeight, chainStatus[chainId].LastIrreversibleBlockHeight);

            var indexerLatestBlock = await _transferProvider.GetLatestBlockAsync(chainId);
            if (indexerLatestBlock != null && indexerLatestBlock.BlockHeight > 0)
            {
                indexerLatestHeight[chainId] = indexerLatestBlock.BlockHeight;
                _logger.LogDebug("TxTimer indexer chainId={ChainId}, Height= {Height}", chainId,
                    indexerLatestBlock.BlockHeight);
            }

            var libHeight = chainStatus[chainId]?.LastIrreversibleBlockHeight ?? 0;
            if (libHeight == 0)
            {
                libHeight = await _transferProvider.GetIndexBlockHeightAsync(chainId);
                _logger.LogDebug("TxTimer confirmed indexer chainId={ChainId}, LibHeight= {libHeight}",
                    chainId, libHeight);
                if (libHeight == 0) continue;
            }

            const int subPageSize = 10;
            var pendingList = State.OrderTransactionDict.Where(t => t.Value.ChainId.Equals(chainId)).ToList();
            var pendingSubLists = SplitList(pendingList, subPageSize);
            foreach (var subList in pendingSubLists)
            {
                var txIds = subList.Select(t => t.Value.TxId).Distinct().ToList();
                var pager = await _transferProvider.GetTokenTransferInfoByTxIdsAsync(txIds, libHeight);
                _logger.LogDebug("TxTimer gql txIds={txIds}, libHeight={Height}, count={count}", 
                    string.Join(CommonConstant.Comma, txIds), libHeight, pager.TotalCount);
                var indexerTxDict = pager.Items.ToDictionary(t => t.TransactionId, t => t);
                var handleResult = await HandlePage(subList, chainStatus, indexerLatestHeight, indexerTxDict);
                foreach (var (orderId, remove) in handleResult)
                {
                    if (!remove) continue;
                    removed++;
                    _logger.LogDebug("OrderTxTimerGrain remove {RemovedOrderId}", orderId);
                    State.OrderTransactionDict.Remove(orderId);
                }

                await WriteStateAsync();
            }
        }

        _logger.LogInformation("OrderTxTimerGrain finish, count: {Removed}/{Total}", removed, total);
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

    private async Task<Dictionary<Guid, bool>> HandlePage(
        List<KeyValuePair<Guid, TimerTransaction>> pendingList, Dictionary<string, ChainStatusDto> chainStatusDict,
        Dictionary<string, long> indexerHeightDict, Dictionary<string, TransferDto> indexerTx)
    {
        _logger.LogDebug("TxTimer handle page, pendingCount={Count}", pendingList.Count);
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
            // 1. The order has been in the list for a long time.
            // 2. indexer services are highly backward and LIB is more
            var queryNode = now > pendingTx.TxTime + _chainOptions.Value.TxResultFromNodeSecondsAfter * 1000;
            if (queryNode || !IndexerAvailable(pendingTx.ChainId, chainStatusDict, indexerHeightDict))
            {
                _logger.LogDebug("TxTimer use node result orderId={OrderId}, chainId={ChainId}, txId={TxId}", orderId,
                    pendingTx.ChainId, pendingTx.TxId);
                result[orderId] = await HandleOrderTransaction(order, pendingTx, chainStatusDict[pendingTx.ChainId]);
                continue;
            }

            _logger.LogDebug("TxTimer use indexer result orderId={OrderId}, chainId={ChainId}, txId={TxId}", orderId,
                pendingTx.ChainId, pendingTx.TxId);
            // When the transaction is just sent to the node,
            // the query may appear NotExisted status immediately, so this is to skip this period of time
            var isToTransfer = pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString();
            var transferInfo = isToTransfer ? order.ToTransfer : order.FromTransfer;
            if (transferInfo.TxTime > DateTime.UtcNow.AddSeconds(5).ToUtcMilliSeconds())
            {
                result[orderId] = false;
                continue;
            }

            if (!indexerTx.ContainsKey(pendingTx.TxId))
            {
                result[orderId] = false;
                var info = await _transferProvider.GetTokenTransferInfoByTxIdsAsync(new List<string> { pendingTx.TxId }, 0);
                if (info.TotalCount > 0)
                {
                    var txBlockHeight = info.Items.FirstOrDefault().BlockHeight;
                    order.ExtensionInfo.AddOrReplace(
                        isToTransfer ? ExtensionKey.ToConfirmedNum : ExtensionKey.FromConfirmedNum,
                        (chainStatusDict[pendingTx.ChainId].BestChainHeight - txBlockHeight).ToString());
                    var direction = isToTransfer ? "to" : "from";
                    _logger.LogDebug(
                        $"TxTimer {direction} confirmedNum, orderId={orderId}, bestHeight={chainStatusDict[pendingTx.ChainId].BestChainHeight}, txBlockHeight={txBlockHeight}, confirmedNum={order.ExtensionInfo[isToTransfer ? ExtensionKey.ToConfirmedNum : ExtensionKey.FromConfirmedNum]}");
                    await SaveOrder(order);
                }
                
                continue;
            }

            // Transfer data from indexer
            _logger.LogDebug("TxTimer indexer transaction exists, orderId={OrderId}, txId={TxId}", orderId,
                pendingTx.TxId);
            var transfer = indexerTx[pendingTx.TxId];
            if (pendingTx.TransferType == TransferTypeEnum.ToTransfer.ToString())
            {
                order.ToTransfer.Status = OrderTransferStatusEnum.Confirmed.ToString();
                order.Status = OrderStatusEnum.ToTransferConfirmed.ToString();
                await ChangeOperationStatus(order);
                order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum,
                    (chainStatusDict[pendingTx.ChainId].BestChainHeight - transfer.BlockHeight).ToString());
                _logger.LogDebug(
                    "TxTimer to confirmedNum, orderId={orderId}, bestHeight={bestHeight}, blockHeight={blockHeight}, confirmedNum={confirmedNum}",
                    orderId, chainStatusDict[pendingTx.ChainId].BestChainHeight, transfer.BlockHeight,
                    order.ExtensionInfo[ExtensionKey.ToConfirmedNum]);
            }
            else
            {
                order.FromTransfer.FromAddress = transfer.FromAddress;
                order.FromTransfer.ToAddress = transfer.ToAddress;
                order.FromTransfer.Status = OrderTransferStatusEnum.Confirmed.ToString();
                order.Status = OrderStatusEnum.FromTransferConfirmed.ToString();
                order.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum,
                    (chainStatusDict[pendingTx.ChainId].BestChainHeight - transfer.BlockHeight).ToString());
                _logger.LogDebug(
                    "TxTimer from confirmedNum, orderId={orderId}, bestHeight={bestHeight}, blockHeight={blockHeight}, confirmedNum={confirmedNum}",
                    orderId, chainStatusDict[pendingTx.ChainId].BestChainHeight, transfer.BlockHeight,
                    order.ExtensionInfo[ExtensionKey.FromConfirmedNum]);
            }
            await SaveOrder(order, ExtensionBuilder.New()
                .Add(ExtensionKey.IsForward, pendingTx.IsForward)
                .Add(ExtensionKey.TransactionStatus, transfer.Status)
                .Build());
            result[orderId] = true;
        }

        return result;
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
            if (transferInfo.TxTime > DateTime.UtcNow.AddSeconds(5).ToUtcMilliSeconds())
            {
                return false;
            }

            var txStatus =
                await _contractProvider.QueryTransactionResultAsync(transferInfo.ChainId, transferInfo.TxId);
            _logger.LogInformation(
                "TxOrderTimer order={OrderId}, txId={TxId}, status={Status}, bestHeight={BestHeight}, txHeight={Height}, LIB={Lib}", 
                order.Id, timerTx.TxId, txStatus.Status, chainStatus.BestChainHeight, txStatus.BlockNumber, 
                chainStatus.LastIrreversibleBlockHeight);

            order.ExtensionInfo.AddOrReplace(isToTransfer ? ExtensionKey.ToConfirmedNum : ExtensionKey.FromConfirmedNum, (chainStatus.BestChainHeight - txStatus.BlockNumber).ToString());

            // pending status, continue waiting
            if (txStatus.Status == CommonConstant.TransactionState.Pending) return false;

            // Transaction is packaged and requires multiple verification
            if (txStatus.Status == CommonConstant.TransactionState.Mined)
            {
                if (transferInfo.Status == OrderTransferStatusEnum.Transferred.ToString())
                {
                    // LIB has not confirmed, continue to wait
                    if (!IsTxConfirmed(txStatus.BlockNumber, chainStatus)) return false;

                    // LIB confirmed, return order to stream
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

                    transferInfo.Status = OrderTransferStatusEnum.Confirmed.ToString();
                    order.Status = isToTransfer
                        ? OrderStatusEnum.ToTransferConfirmed.ToString()
                        : OrderStatusEnum.FromTransferConfirmed.ToString();

                    await SaveOrder(order, ExtensionBuilder.New()
                        .Add(ExtensionKey.IsForward, timerTx.IsForward)
                        .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                        .Add(ExtensionKey.TransactionError, txStatus.Error)
                        .Build());
                    return true;
                }

                // transaction status not mined before, save new status of tx
                _logger.LogInformation("TxOrderTimer transaction status not mined");
                transferInfo.Status = OrderTransferStatusEnum.Transferred.ToString();
                order.Status = isToTransfer
                    ? OrderStatusEnum.ToTransferred.ToString()
                    : OrderStatusEnum.FromTransferred.ToString();

                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                return false;
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
                    .Add(ExtensionKey.IsForward, timerTx.IsForward)
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                _logger.LogWarning(
                    "TxOrderTimer tx status {TxStatus}, orderId={OrderId}. txId={TxId}, error={Error}", 
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
                _logger.LogError(e, "TxOrderTimer timer tx expired, orderId={OrderId}, txId={TxId}, txTime={Time}",
                    order.Id,
                    timerTx.TxTime, timerTx.TxTime);
                return true;
            }

            _logger.LogError(e, "TxOrderTimer timer handle tx failed, orderId={OrderId}, txId={TxId}, txTime={Time}",
                order.Id,
                timerTx.TxTime, timerTx.TxTime);
            return false;
        }
    }

    private async Task ChangeOperationStatus(TOrder order, bool success = true)
    {
        order.ExtensionInfo ??= new Dictionary<string, string>();
        if (order.OrderType == OrderTypeEnum.Deposit.ToString())
        {
            if (!order.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)) return;
            
            order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, success
                ? OrderOperationStatusEnum.ReleaseConfirmed.ToString()
                : OrderOperationStatusEnum.ReleaseFailed.ToString());
            
        }
        if (order.OrderType == OrderTypeEnum.Withdraw.ToString())
        {
            if (!order.ExtensionInfo.ContainsKey(ExtensionKey.RelatedOrderId)) return;
            var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(Guid.Parse(order.ExtensionInfo[ExtensionKey.RelatedOrderId]));
            var res = await recordGrain.Get();
            if (res.Success)
            {
                var orderRelated = res.Value;
                if (orderRelated.ExtensionInfo.IsNullOrEmpty() || !orderRelated.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)) return;

                orderRelated.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, success
                    ? OrderOperationStatusEnum.RefundConfirmed.ToString()
                    : OrderOperationStatusEnum.RefundFailed.ToString());
                await recordGrain.AddOrUpdate(orderRelated);
                await _userWithdrawProvider.AddOrUpdateSync(orderRelated);
            }
        }
    }

    private async Task<Tuple<string, string>> ParseFromLogEvent(LogEventDto[] logs)
    {
        try
        {
            var param = TokenPoolTransferred.Parser.ParseFrom(
                ByteString.FromBase64(logs.FirstOrDefault(l => l.Name == nameof(TokenPoolTransferred))?.NonIndexed));
            _logger.LogInformation(
                "TxOrderTimer ParseFromLogEvent, from={from}, to={to}", param.From.ToBase58(), param.To.ToBase58());
            return Tuple.Create(param.From.ToBase58(), param.To.ToBase58());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TxOrderTimer ParseFromLogEvent error");
            return Tuple.Create(string.Empty, string.Empty);
        }
    }
    
    private bool IndexerAvailable(string chainId, Dictionary<string, ChainStatusDto> chainStatus,
        Dictionary<string, long> indexerBlockHeight)
    {
        var statusExists = chainStatus.TryGetValue(chainId, out var status);
        var heightExists = indexerBlockHeight.TryGetValue(chainId, out var indexerHeight);
        return statusExists && heightExists &&
               status.LastIrreversibleBlockHeight - _chainOptions.Value.IndexerAvailableHeightBehind <
               indexerHeight;
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
            AssertHelper.NotNull(order, "TxTimer Query order empty");
            return order;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("TxTimer order timer error, OrderId={OrderId} Message={Msg}", orderId, e.Message);
            return null;
        }
    }


    // Transaction height less than LIB, or less than the specified number of blocks, is considered confirmed
    private bool IsTxConfirmed(long txBlockNumber, ChainStatusDto chainStatus)
    {
        var confirmBlocks = _chainOptions.Value.Contract.SafeBlockHeight;
        return txBlockNumber < chainStatus.LastIrreversibleBlockHeight ||
               txBlockNumber < chainStatus.BestChainHeight - confirmBlocks;
    }
}