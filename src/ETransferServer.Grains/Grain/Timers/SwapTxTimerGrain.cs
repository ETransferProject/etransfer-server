using AElf.Client.Dto;
using ETransfer.Contracts.TokenPool;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.GraphQL;
using Microsoft.Extensions.Logging;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Swap;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
using NBitcoin;
using Newtonsoft.Json;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface ISwapTxTimerGrain : IBaseTxTimerGrain
{
}

public class SwapTxTimerGrain : Grain<OrderSwapTimerState>, ISwapTxTimerGrain
{
    internal DateTime LastCallBackTime;

    private readonly ILogger<SwapTxTimerGrain> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ITokenTransferProvider _transferProvider;
    private readonly IUserDepositProvider _userDepositProvider;
    
    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    public SwapTxTimerGrain(ILogger<SwapTxTimerGrain> logger, 
        IContractProvider contractProvider,
        IUserDepositProvider userDepositProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, 
        IOptionsSnapshot<TimerOptions> timerOptions,
        ITokenTransferProvider transferProvider)
    {
        _logger = logger;
        _contractProvider = contractProvider;
        _userDepositProvider = userDepositProvider;
        _chainOptions = chainOptions;
        _timerOptions = timerOptions;
        _transferProvider = transferProvider;
    }


    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(LastCallBackTime);
    }
    
    public async Task AddToPendingList(Guid id, TimerTransaction transaction)
    {
        if (State.OrderTransactionDict.ContainsKey(id))
        {
            _logger.LogWarning("Order id {Id} exists in SwapTxTimerGrain state", id);
            return;
        }

        _logger.LogInformation("Order id {Id} AddToPendingList before paramCheck", id);
        AssertHelper.NotNull(transaction, "Transaction empty");
        AssertHelper.NotEmpty(transaction.ChainId, "Transaction chainId empty");
        AssertHelper.NotEmpty(transaction.TxId, "Transaction id empty");
        AssertHelper.NotNull(transaction.TxTime, "Transaction time null");

        _logger.LogInformation("Order id {Id} AddToPendingList after paramCheck", id);
        State.OrderTransactionDict[id] = transaction;

        await WriteStateAsync();
    }
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SwapTxTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);

        _logger.LogDebug("SwapTxTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.PeriodSeconds)
        );
    }
    
    internal async Task TimerCallback(object state)
    {
        var total = State.OrderTransactionDict.Count;
        _logger.LogDebug("SwapTxTimerGrain callback, Total={Total}", total);
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
            {
                _logger.LogDebug("SwapTxTimer node chainId={ChainId}, Height= {Height}, LibHeight= {libHeight}", 
                    chainId, chainStatus[chainId].LongestChainHeight, chainStatus[chainId].LastIrreversibleBlockHeight);

                indexerLatestHeight[chainId] = chainStatus[chainId].BestChainHeight;
                _logger.LogDebug("SwapTxTimer indexer chainId={ChainId}, Height= {Height}", chainId,
                    chainStatus[chainId].BestChainHeight);
            }
            
            var libHeight = chainStatus[chainId]?.LastIrreversibleBlockHeight ?? 0;
            if (libHeight == 0)
            {
                libHeight = await _transferProvider.GetIndexBlockHeightAsync(chainId);
                _logger.LogDebug("SwapTxTimer confirmed indexer chainId={ChainId}, LibHeight= {libHeight}",
                    chainId, libHeight);
                if (libHeight == 0) continue;
            }
            
            const int subPageSize = 10;
            var pendingList = State.OrderTransactionDict.Where(t => t.Value.ChainId.Equals(chainId)).ToList();
            _logger.LogInformation("pendingList {distinctPendingList}", JsonConvert.SerializeObject(pendingList));
            var distinctPendingList = pendingList
                .GroupBy(p => p.Value.TxId)
                .Select(g => g.First())
                .ToList();
            _logger.LogInformation("distinctPendingList {distinctPendingList}",
                JsonConvert.SerializeObject(distinctPendingList));
            var pendingSubLists = SplitList(distinctPendingList, subPageSize);
            foreach (var subList in pendingSubLists)
            {
                var txIds = subList.Select(t => t.Value.TxId).Distinct().ToList();
                var pager = await _transferProvider.GetSwapTokenInfoByTxIdsAsync(txIds, libHeight);
                _logger.LogDebug("SwapTxTimer gql txIds={txIds}, libHeight={Height}, count={count}", 
                    string.Join(CommonConstant.Comma, txIds), libHeight, pager.TotalCount);
                var indexerTxDict = pager.Items.ToDictionary(t => t.TransactionId, t => t);
                var handleResult = await HandlePage(subList, chainStatus, indexerLatestHeight, indexerTxDict);
                _logger.LogInformation("handleResult {handleResult}", JsonConvert.SerializeObject(handleResult));
                foreach (var (orderId, remove) in handleResult)
                {
                    if (!remove) continue;
                    removed++;
                    _logger.LogDebug("SwapTxTimerGrain remove {RemovedOrderId}", orderId);
                    State.OrderTransactionDict.Remove(orderId);
                }

                await WriteStateAsync();
            }
        }

        _logger.LogInformation("SwapTxTimerGrain finish, count: {Removed}/{Total}", removed, total);
    }
    
    private IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 10)
    {
        for (var i = 0; i < locations.Count; i += nSize)
        {
            yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
        }
    }

    private async Task<Dictionary<Guid, bool>> HandlePage(
        List<KeyValuePair<Guid, TimerTransaction>> pendingList, Dictionary<string, ChainStatusDto> chainStatusDict,
        Dictionary<string, long> indexerHeightDict, Dictionary<string, SwapRecordDto> indexerTx)
    {
        _logger.LogDebug("SwapTxTimer handle page, pendingCount={Count}", pendingList.Count);
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var result = new Dictionary<Guid, bool>();
        foreach (var (orderId, pendingTx) in pendingList)
        {
            // query order and verify pendingTx data
            var order = await QueryOrderAndVerify(orderId, pendingTx);
            _logger.LogInformation("QueryOrderAndVerify {order}", JsonConvert.SerializeObject(order));
            if (order == null)
            {
                result[orderId] = true;
                continue;
            }

            // The following two cases directly from the node query results
            // 1. The order has been in the list for a long time.
            var queryNode = now > pendingTx.TxTime + _chainOptions.Value.TxResultFromNodeSecondsAfter * 1000;
            _logger.LogInformation("queryNode {queryNode}, now: {now}, pendingTx.TxTime: {txTime}", queryNode, now, pendingTx.TxTime);
            if (queryNode || !IndexerAvailable(pendingTx.ChainId, chainStatusDict, indexerHeightDict))
            {
                _logger.LogDebug("SwapTxTimer use node result orderId={OrderId}, chainId={ChainId}, txId={TxId}", 
                    orderId, pendingTx.ChainId, pendingTx.TxId);
                result[orderId] = await HandleOrderTransaction(order, pendingTx, chainStatusDict[pendingTx.ChainId]);
                continue;
            }
            
            _logger.LogDebug("SwapTxTimer use indexer result orderId={OrderId}, chainId={ChainId}, txId={TxId}", 
                orderId, pendingTx.ChainId, pendingTx.TxId);
            // When the transaction is just sent to the node,
            // the query may appear NotExisted status immediately, so this is to skip this period of time
            var transferInfo = order.ToTransfer;
            if (transferInfo.TxTime > DateTime.UtcNow.AddSeconds(5).ToUtcMilliSeconds())
            {
                result[orderId] = false;
                continue;
            }

            if (!indexerTx.ContainsKey(pendingTx.TxId))
            {
                result[orderId] = false;
                var info = await _transferProvider.GetSwapTokenInfoByTxIdsAsync(new List<string> { pendingTx.TxId }, 0);
                if (info.TotalCount > 0)
                {
                    var txBlockHeight = info.Items.FirstOrDefault().BlockHeight;
                    order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum,
                        (chainStatusDict[pendingTx.ChainId].BestChainHeight - txBlockHeight).ToString());
                    _logger.LogDebug(
                        "TxTimer to confirmedNum, orderId={orderId}, bestHeight={bestHeight}, txBlockHeight={txBlockHeight}, confirmedNum={confirmedNum}",
                        orderId, chainStatusDict[pendingTx.ChainId].BestChainHeight, txBlockHeight,
                        order.ExtensionInfo[ExtensionKey.ToConfirmedNum]);
                    await SaveOrder(order);
                }
                continue;
            }

            // Transfer data from indexer
            _logger.LogDebug("SwapTxTimer indexer transaction exists, orderId={OrderId}, txId={TxId}", 
                orderId, pendingTx.TxId);
            var transfer = indexerTx[pendingTx.TxId];
            var swapGrain = GrainFactory.GetGrain<ISwapGrain>(order.Id);
            order.ToTransfer.Amount =  await swapGrain.RecordAmountOut(transfer.AmountOut);
            _logger.LogInformation("SwapTxTimer toTransfer amount: {Amount}", order.ToTransfer.Amount);
            order.ToTransfer.Status = OrderTransferStatusEnum.Confirmed.ToString();
            order.Status = OrderStatusEnum.ToTransferConfirmed.ToString();
            order.ExtensionInfo ??= new Dictionary<string, string>();
            if (order.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus))
            {
                order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
                    OrderOperationStatusEnum.ReleaseConfirmed.ToString());
            }
            order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum,
                (chainStatusDict[pendingTx.ChainId].BestChainHeight - transfer.BlockHeight).ToString());
            _logger.LogDebug(
                "SwapTxTimer to confirmedNum, orderId={orderId}, bestHeight={bestHeight}, blockHeight={blockHeight}, confirmedNum={confirmedNum}",
                orderId, chainStatusDict[pendingTx.ChainId].BestChainHeight, transfer.BlockHeight,
                order.ExtensionInfo[ExtensionKey.ToConfirmedNum]);

            await SaveOrder(order, ExtensionBuilder.New()
                .Add(ExtensionKey.TransactionStatus, CommonConstant.TransactionState.Mined)
                .Build());
            result[orderId] = true;
        }

        return result;
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
    
    private async Task<DepositOrderDto> QueryOrderAndVerify(Guid orderId, TimerTransaction timerTx)
    {
        DepositOrderDto order;
        try
        {
            /*
             * Check the abnormal data, these exceptions should not occur,
             * if so, these data should be kicked out of the list and discarded.
             */
            order = await GetOrder(orderId);
            AssertHelper.NotNull(order, "SwapTxTimer Query order empty");
            return order;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("SwapTxTimer order timer error, OrderId={OrderId} Message={Msg}", orderId, e.Message);
            return null;
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
    internal async Task<bool> HandleOrderTransaction(DepositOrderDto order, TimerTransaction timerTx, ChainStatusDto chainStatus)
    {
        var txDateTime = TimeHelper.GetDateTimeFromTimeStamp(timerTx.TxTime ?? 0);
        var txExpireTime = txDateTime.AddSeconds(_chainOptions.Value.Contract.TransactionTimerMaxSeconds);

        var transferInfo = order.ToTransfer;;
        
        _logger.LogInformation("HandleOrderTransaction: {order}", JsonConvert.SerializeObject(order));

        var swapId = order.ToTransfer.TxId;
        var swapTxTime = transferInfo.TxTime;
        
        _logger.LogInformation("HandleOrderTransaction after set param: {order}", JsonConvert.SerializeObject(order));
        
        try
        {
            
            AssertHelper.NotNull(transferInfo, "Order transfer not found, txId={TxId}", timerTx.TxId);
            AssertHelper.IsTrue(swapId == timerTx.TxId,
                "Timer txId not match, expected={TimerTxId}, actually={TxId}", timerTx.TxId, swapId);
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
            if (swapTxTime > DateTime.UtcNow.AddSeconds(5).ToUtcMilliSeconds())
            {
                return false;
            }

            var txStatus =
                await _contractProvider.QueryTransactionResultAsync(transferInfo.ChainId, swapId);
            _logger.LogInformation(
                "TxOrderTimer order={OrderId}, txId={TxId}, status={Status}, txHeight={Height}, LIB={Lib}, returnValue={ReturnValue}", order.Id,
                timerTx.TxId, txStatus.Status, txStatus.BlockNumber, chainStatus.LastIrreversibleBlockHeight, txStatus.ReturnValue);

            order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum, (chainStatus.BestChainHeight - txStatus.BlockNumber).ToString());

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
                    transferInfo.Status = OrderTransferStatusEnum.Confirmed.ToString();
                    order.Status = OrderStatusEnum.ToTransferConfirmed.ToString();
                    var swapGrain = GrainFactory.GetGrain<ISwapGrain>(order.Id);
                    transferInfo.Amount = 0;
                    if (txStatus.Logs.Length > 0)
                    {
                        var swapLog = txStatus.Logs.FirstOrDefault(l => l.Name == nameof(TokenSwapped))?.NonIndexed;
                        transferInfo.Amount = await swapGrain.ParseReturnValue(swapLog);
                    }

                    order.ExtensionInfo ??= new Dictionary<string, string>();
                    if (order.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus))
                    {
                        order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
                            OrderOperationStatusEnum.ReleaseConfirmed.ToString());
                    }

                    _logger.LogInformation("After ParseReturnValueAsync: {Amount}", transferInfo.Amount);

                    await SaveOrder(order, ExtensionBuilder.New()
                        .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                        .Add(ExtensionKey.TransactionError, txStatus.Error)
                        .Build());
                    return true;
                }

                // transaction status not mined before, save new status of tx
                transferInfo.Status = OrderTransferStatusEnum.Transferred.ToString();
                order.Status = OrderStatusEnum.ToTransferred.ToString();

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
                // transferInfo.Status = OrderTransferStatusEnum.Failed.ToString();
                // order.Status = OrderStatusEnum.ToTransferFailed.ToString();

                _logger.LogInformation(
                    "SwapTx result Confirmed failed, will call ToStartTransfer, status: {result}, order: {order}",
                    txStatus.Status, JsonConvert.SerializeObject(order));

                await DepositSwapFailureAlarmAsync(order, SwapStage.SwapTxHandleFailAndToTransfer);

                transferInfo.Status = OrderTransferStatusEnum.StartTransfer.ToString();
                order.Status = OrderStatusEnum.ToStartTransfer.ToString();
                transferInfo.Symbol = order.FromTransfer.Symbol;
                order.FromRawTransaction = null;
                transferInfo.TxId = null;
                transferInfo.TxTime = null;
                order.ExtensionInfo[ExtensionKey.NeedSwap] = Boolean.FalseString;
                order.ExtensionInfo[ExtensionKey.SwapStage] = SwapStage.SwapTxHandleFailAndToTransfer;
                order.ExtensionInfo[ExtensionKey.ToConfirmedNum] = "0";

                _logger.LogInformation("Before calling the ToStartTransfer method, after resetting the properties of the order, order: {order}",
                    JsonConvert.SerializeObject(order));
                
                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                _logger.LogWarning(
                    "SwapTxOrderTimer tx status {TxStatus}, orderId={OrderId}. txId={TxId}, error={Error}", 
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
                _logger.LogError(e, "SwapTxOrderTimer timer tx expired, orderId={OrderId}, txId={TxId}, txTime={Time}",
                    order.Id,
                    timerTx.TxTime, timerTx.TxTime);
                return true;
            }

            _logger.LogError(e, "SwapTxOrderTimer timer handle tx failed, orderId={OrderId}, txId={TxId}, txTime={Time}",
                order.Id,
                timerTx.TxTime, timerTx.TxTime);
            return false;
        }
    }
    
    // Transaction height less than LIB, or less than the specified number of blocks, is considered confirmed
    private bool IsTxConfirmed(long txBlockNumber, ChainStatusDto chainStatus)
    {
        var confirmBlocks = _chainOptions.Value.Contract.SafeBlockHeight;
        return txBlockNumber < chainStatus.LastIrreversibleBlockHeight ||
               txBlockNumber < chainStatus.BestChainHeight - confirmBlocks;
    }
    
    private async Task SaveOrder(DepositOrderDto order)
    {
        var recordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(order.Id);
        var res = await recordGrain.CreateOrUpdateAsync(order);
        await _userDepositProvider.AddOrUpdateSync(res.Value);
    }
    
    private async Task SaveOrder(DepositOrderDto order, Dictionary<string, string> externalInfo)
    {
        var userDepositGrain = GrainFactory.GetGrain<IUserDepositGrain>(order.Id);
        await userDepositGrain.AddOrUpdateOrder(order, externalInfo);
    }

    private async Task<DepositOrderDto> GetOrder(Guid orderId)
    {
        var userDepositRecord = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
        var resp = await userDepositRecord.GetAsync();
        if (!resp.Success)
        {
            _logger.LogWarning("Swap Deposit order {OrderId} not found", orderId);
            return null;
        }
        return resp.Data as DepositOrderDto;
    }
    
    private async Task DepositSwapFailureAlarmAsync(DepositOrderDto orderDto, string reason)
    {
        var depositSwapMonitorGrain = GrainFactory.GetGrain<IDepositSwapMonitorGrain>(orderDto.Id.ToString());
        await depositSwapMonitorGrain.DoMonitor(DepositSwapMonitorDto.Create(orderDto, reason));
    }
}