using AElf.Client.Dto;
using ETransfer.Contracts.TokenPool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Swap;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Newtonsoft.Json;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface ISwapTxFastTimerGrain : IGrainWithGuidKey
{
    public Task AddToPendingList(Guid id, TimerTransaction transaction);

    public Task<DateTime> GetLastCallBackTime();
}

public class SwapTxFastTimerGrain: Grain<OrderSwapFastTimerState>, ISwapTxFastTimerGrain
{
    internal DateTime LastCallBackTime;

    private readonly ILogger<SwapTxFastTimerGrain> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly ITokenTransferProvider _transferProvider;
    private readonly IUserDepositProvider _userDepositProvider;

    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    public SwapTxFastTimerGrain(ILogger<SwapTxFastTimerGrain> logger,
        IContractProvider contractProvider,
        IUserDepositProvider userDepositProvider,
        IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        IOptionsSnapshot<TimerOptions> timerOptions,
        ITokenTransferProvider transferProvider)
    {
        _logger = logger;
        _contractProvider = contractProvider;
        _transferProvider = transferProvider;
        _userDepositProvider = userDepositProvider;
        _chainOptions = chainOptions;
        _withdrawOptions = withdrawOptions;
        _timerOptions = timerOptions;
    }
    
    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(LastCallBackTime);
    }

    public async Task AddToPendingList(Guid id, TimerTransaction transaction)
    {
        if (State.OrderTransactionDict.ContainsKey(id))
        {
            _logger.LogWarning("Order id {Id} exists in SwapTxFastTimerGrain state", id);
            return;
        }

        AssertHelper.NotNull(transaction, "Transaction empty");
        AssertHelper.NotEmpty(transaction.ChainId, "Transaction chainId empty");
        AssertHelper.NotEmpty(transaction.TxId, "Transaction id empty");
        AssertHelper.NotNull(transaction.TxTime, "Transaction time null");

        State.OrderTransactionDict[id] = transaction;

        await WriteStateAsync();
    }
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SwapTxFastTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);

        _logger.LogDebug("SwapTxFastTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.SwapFastTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.SwapFastTimer.PeriodSeconds)
        );
    }

    private async Task TimerCallback(object state)
    {
        var total = State.OrderTransactionDict.Count;
        _logger.LogDebug("SwapTxFastTimerGrain callback, Total={Total}", total);
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
                _logger.LogDebug("SwapTxFastTimer node chainId={ChainId}, Height={Height}", chainId,
                    chainStatus[chainId].LongestChainHeight);
        }
        
        var removed = 0;
        const int subPageSize = 10;
        var pendingList = State.OrderTransactionDict.ToList();
        var pendingSubLists = SplitList(pendingList, subPageSize);
        foreach (var subList in pendingSubLists)
        {
            var txIds = subList.Select(t => t.Value.TxId).Distinct().ToList();
            var pager = await _transferProvider.GetSwapTokenInfoByTxIdsAsync(txIds, 0);
            _logger.LogDebug("SwapTxFastTimer gql txIds={txIds}, count={count}", 
                string.Join(CommonConstant.Comma, txIds), pager.TotalCount);
            var indexerTxDict = pager.Items.ToDictionary(t => t.TransactionId, t => t);
            var handleResult = await HandlePage(subList, chainStatus, indexerTxDict);
            _logger.LogInformation("handleResult {handleResult}", JsonConvert.SerializeObject(handleResult));
            foreach (var (orderId, remove) in handleResult)
            {
                if (!remove) continue;
                removed++;
                _logger.LogDebug("SwapTxFastTimerGrain remove {RemovedOrderId}", orderId);
                State.OrderTransactionDict.Remove(orderId);
            }

            await WriteStateAsync();
        }

        _logger.LogInformation("SwapTxFastTimerGrain finish, count: {Removed}/{Total}", removed, total);
    }

    private IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 10)
    {
        for (var i = 0; i < locations.Count; i += nSize)
        {
            yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
        }
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
        var recordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
        var res = await recordGrain.GetAsync();
        if (!res.Success)
        {
            _logger.LogWarning("Deposit order {OrderId} not found", orderId);
            return null;
        }
        return res.Data as DepositOrderDto;
    }

    private async Task<Dictionary<Guid, bool>> HandlePage(List<KeyValuePair<Guid, TimerTransaction>> pendingList, 
        Dictionary<string, ChainStatusDto> chainStatusDict, Dictionary<string, SwapRecordDto> indexerTx)
    {
        _logger.LogDebug("SwapTxFastTimer handle page, pendingCount={Count}", pendingList.Count);
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var result = new Dictionary<Guid, bool>();
        foreach (var (orderId, pendingTx) in pendingList)
        {
            // query order and verify pendingTx data
            var order = await QueryOrderAndVerify(orderId);
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
                _logger.LogDebug("SwapTxFastTimer use node result orderId={OrderId}, chainId={ChainId}, txId={TxId}", 
                    orderId, pendingTx.ChainId, pendingTx.TxId);
                result[orderId] = await HandleOrderTransaction(order, pendingTx, chainStatusDict[pendingTx.ChainId]);
                continue;
            }

            _logger.LogDebug("SwapTxFastTimer use indexer result orderId={OrderId}, chainId={ChainId}, txId={TxId}, " +
                "indexerTxCount={Count}", orderId, pendingTx.ChainId, pendingTx.TxId, indexerTx.Count);
            if (!indexerTx.ContainsKey(pendingTx.TxId))
            {
                result[orderId] = false;
                continue;
            }
            
            if (!order.ExtensionInfo.ContainsKey(ExtensionKey.SwapTxId) ||
                order.ExtensionInfo[ExtensionKey.SwapTxId].IsNullOrEmpty())
            {
                var transfer = indexerTx[pendingTx.TxId];
                order.ExtensionInfo[ExtensionKey.SwapTxId] = pendingTx.TxId;
                var swapGrain = GrainFactory.GetGrain<ISwapGrain>(order.Id);
                order.ToTransfer.Amount =  await swapGrain.RecordAmountOut(transfer.AmountOut);
                _logger.LogInformation("SwapTxFastTimer toTransfer amount: {Amount}", order.ToTransfer.Amount);
                await SaveOrder(order);
            }
            
            //fast confirmed
            if (!IsTxFastConfirmed(pendingTx, order, indexerTx, chainStatusDict[pendingTx.ChainId]))
            {
                result[orderId] = false;
                continue;
            }

            // Transfer data from indexer
            _logger.LogDebug("SwapTxFastTimer indexer transaction exists, orderId={OrderId}, txId={TxId}", orderId,
                pendingTx.TxId);
            await SaveOrderTxFlowAsync(order, pendingTx.TxId, ThirdPartOrderStatusEnum.Success.ToString());
            order.ToTransfer.FromAddress = order.ExtensionInfo[ExtensionKey.SwapFromAddress];
            order.ToTransfer.ToAddress = order.ExtensionInfo[ExtensionKey.SwapToAddress];
            order.ToTransfer.ChainId = order.ExtensionInfo[ExtensionKey.SwapChainId];
            order.ToTransfer.Status = OrderTransferStatusEnum.StartTransfer.ToString();
            order.Status = OrderStatusEnum.ToStartTransfer.ToString();
            order.FromRawTransaction = null;
            order.ToTransfer.TxId = null;
            order.ToTransfer.TxTime = null;
            order.ExtensionInfo[ExtensionKey.NeedSwap] = Boolean.FalseString;
            order.ExtensionInfo[ExtensionKey.ToConfirmedNum] = "0";
            _logger.LogDebug("SwapTxFastTimer start to mainChain, dto={dto}", JsonConvert.SerializeObject(order));
            await SaveOrder(order, ExtensionBuilder.New()
                .Add(ExtensionKey.TransactionStatus, CommonConstant.TransactionState.Mined)
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
    private async Task<bool> HandleOrderTransaction(DepositOrderDto order, TimerTransaction timerTx, ChainStatusDto chainStatus)
    {
        var txDateTime = TimeHelper.GetDateTimeFromTimeStamp(timerTx.TxTime ?? 0);
        var txExpireTime = txDateTime.AddSeconds(_chainOptions.Value.Contract.TransactionTimerMaxSeconds);

        var transferInfo = order.ToTransfer;
        try
        {
            /*
             * Check the abnormal data, these exceptions should not occur,
             * if so, these data should be kicked out of the list and discarded.
             */
            AssertHelper.NotNull(transferInfo, "Order transfer not found, txId={TxId}", timerTx.TxId);
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
                await _contractProvider.QueryTransactionResultAsync(transferInfo.ChainId, timerTx.TxId);
            _logger.LogInformation(
                "SwapTxFastTimer order={OrderId}, txId={TxId}, status={Status}, bestHeight={BestHeight}, txHeight={Height}, LIB={Lib}", 
                order.Id, timerTx.TxId, txStatus.Status, chainStatus.BestChainHeight, txStatus.BlockNumber, 
                chainStatus.LastIrreversibleBlockHeight);

            // pending status, continue waiting
            if (txStatus.Status == CommonConstant.TransactionState.Pending) return false;

            // Transaction is packaged and requires multiple verification
            if (txStatus.Status == CommonConstant.TransactionState.Mined)
            {
                if (!order.ExtensionInfo.ContainsKey(ExtensionKey.SwapTxId) ||
                    order.ExtensionInfo[ExtensionKey.SwapTxId].IsNullOrEmpty())
                {
                    var swapGrain = GrainFactory.GetGrain<ISwapGrain>(order.Id);
                    transferInfo.Amount = 0;
                    if (txStatus.Logs.Length > 0)
                    {
                        order.ExtensionInfo[ExtensionKey.SwapTxId] = timerTx.TxId;
                        var swapLog = txStatus.Logs.FirstOrDefault(l => l.Name == nameof(TokenSwapped))?.NonIndexed;
                        transferInfo.Amount = await swapGrain.ParseReturnValue(swapLog);
                    }
                    _logger.LogInformation("After ParseReturnValueAsync: {Amount}", transferInfo.Amount);
                    await SaveOrder(order);
                }
                
                if (!IsTxFastConfirmed(order, txStatus.BlockNumber, chainStatus)) return false;
                
                await SaveOrderTxFlowAsync(order, timerTx.TxId, ThirdPartOrderStatusEnum.Success.ToString());
                order.ToTransfer.FromAddress = order.ExtensionInfo[ExtensionKey.SwapFromAddress];
                order.ToTransfer.ToAddress = order.ExtensionInfo[ExtensionKey.SwapToAddress];
                order.ToTransfer.ChainId = order.ExtensionInfo[ExtensionKey.SwapChainId];
                order.ToTransfer.Status = OrderTransferStatusEnum.StartTransfer.ToString();
                order.Status = OrderStatusEnum.ToStartTransfer.ToString();
                order.FromRawTransaction = null;
                order.ToTransfer.TxId = null;
                order.ToTransfer.TxTime = null;
                order.ExtensionInfo[ExtensionKey.NeedSwap] = Boolean.FalseString;
                order.ExtensionInfo[ExtensionKey.ToConfirmedNum] = "0";
                _logger.LogDebug("SwapTxFastTimer start to mainChain, dto={dto}", JsonConvert.SerializeObject(order));
                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, CommonConstant.TransactionState.Mined)
                    .Build());
                return true;
            }

            if (txStatus.Status == CommonConstant.TransactionState.Failed
                || txStatus.Status == CommonConstant.TransactionState.NodeValidationFailed
                || txStatus.Status == CommonConstant.TransactionState.NotExisted)
            {
                _logger.LogInformation(
                    "SwapTxFastTimer result Confirmed failed, will call ToStartTransfer, status: {result}, order: {order}",
                    txStatus.Status, JsonConvert.SerializeObject(order));

                await SaveOrderTxFlowAsync(order, timerTx.TxId, ThirdPartOrderStatusEnum.Fail.ToString());
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
                order.ExtensionInfo[ExtensionKey.SwapTxId] = timerTx.TxId;
                order.ToTransfer.FromAddress = order.ExtensionInfo[ExtensionKey.SwapOriginFromAddress];
                order.ToTransfer.ToAddress = order.ExtensionInfo[ExtensionKey.SwapToAddress];
                order.ToTransfer.ChainId = order.ExtensionInfo[ExtensionKey.SwapChainId];

                _logger.LogInformation("Before calling the ToStartTransfer method, after resetting the properties of the order, order: {order}",
                    JsonConvert.SerializeObject(order));
                
                await SaveOrder(order, ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txStatus.Status)
                    .Add(ExtensionKey.TransactionError, txStatus.Error)
                    .Build());
                _logger.LogWarning(
                    "SwapTxFastTimer tx status {TxStatus}, orderId={OrderId}. txId={TxId}, error={Error}", 
                    txStatus.Status, order.Id, timerTx.TxId, txStatus.Error);

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
                _logger.LogError(e, "SwapTxFastTimer timer tx expired, orderId={OrderId}, txId={TxId}, txTime={Time}",
                    order.Id, timerTx.TxTime, timerTx.TxTime);
                return true;
            }

            _logger.LogError(e, "SwapTxFastTimer timer handle tx failed, orderId={OrderId}, txId={TxId}, txTime={Time}",
                order.Id, timerTx.TxTime, timerTx.TxTime);
            return false;
        }
    }

    private async Task SaveOrderTxFlowAsync(DepositOrderDto order, string txId, string status)
    {
        if (txId.IsNullOrEmpty()) return;
        var orderTxFlowGrain = GrainFactory.GetGrain<IOrderTxFlowGrain>(order.Id);
        await orderTxFlowGrain.AddOrUpdate(new OrderTxData
        {
            TxId = txId,
            ChainId = order.ToTransfer.ChainId,
            Status = status
        });
    }
    
    private async Task DepositSwapFailureAlarmAsync(DepositOrderDto orderDto, string reason)
    {
        var depositSwapMonitorGrain = GrainFactory.GetGrain<IDepositSwapMonitorGrain>(orderDto.Id.ToString());
        await depositSwapMonitorGrain.DoMonitor(DepositSwapMonitorDto.Create(orderDto, reason));
    }

    private async Task<DepositOrderDto> QueryOrderAndVerify(Guid orderId)
    {
        DepositOrderDto order;
        try
        {
            /*
             * Check the abnormal data, these exceptions should not occur,
             * if so, these data should be kicked out of the list and discarded.
             */
            order = await GetOrder(orderId);
            AssertHelper.NotNull(order, "SwapTxFastTimer Query order empty");
            return order;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("SwapTxFastTimer order timer error, OrderId={OrderId} Message={Msg}", orderId, e.Message);
            return null;
        }
    }

    private bool IsTxFastConfirmed(TimerTransaction pendingTx, DepositOrderDto order,
        Dictionary<string, SwapRecordDto> indexerTx, ChainStatusDto chainStatus)
    {
        try
        {
            (var amount, var symbol, var amountThreshold, var blockHeightUpperThreshold,
                var blockHeightLowerThreshold) = GetTxFastTransactionData(order);

            var indexerHeight = chainStatus.BestChainHeight;
            var txBlockHeight = indexerTx[pendingTx.TxId].BlockHeight;

            _logger.LogDebug(
                "SwapTxFastTimer IsTxFastConfirmed: amount={amount}, symbol={symbol}, amountThreshold={amountThreshold}, " +
                "blockHeightUpperThreshold={blockHeightUpperThreshold}, blockHeightLowerThreshold={blockHeightLowerThreshold}, " +
                "indexerHeight={indexerHeight}, txBlockHeight={txBlockHeight}", amount, symbol, amountThreshold,
                blockHeightUpperThreshold, blockHeightLowerThreshold, indexerHeight, txBlockHeight);

            return (amount > amountThreshold && indexerHeight - txBlockHeight >= blockHeightUpperThreshold)
                   || (amount <= amountThreshold && indexerHeight - txBlockHeight >= blockHeightLowerThreshold);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("SwapTxFastTimer IsTxFastConfirmed error, OrderId={OrderId} Message={Msg}", 
                order.Id, e.Message);
            return false;
        }
    }

    private bool IsTxFastConfirmed(DepositOrderDto order, long txBlockNumber, ChainStatusDto chainStatus)
    {
        try
        {
            (var amount, var symbol, var amountThreshold, var blockHeightUpperThreshold,
                var blockHeightLowerThreshold) = GetTxFastTransactionData(order);

            var indexerHeight = chainStatus.BestChainHeight;

            _logger.LogDebug(
                "SwapTxFastTimer IsTxFastConfirmed: amount={amount}, symbol={symbol}, amountThreshold={amountThreshold}, " +
                "blockHeightUpperThreshold={blockHeightUpperThreshold}, blockHeightLowerThreshold={blockHeightLowerThreshold}, " +
                "indexerHeight={indexerHeight}, txBlockNumber={txBlockNumber}", amount, symbol, amountThreshold,
                blockHeightUpperThreshold, blockHeightLowerThreshold, indexerHeight, txBlockNumber);

            return (amount > amountThreshold && indexerHeight - txBlockNumber >= blockHeightUpperThreshold)
                   || (amount <= amountThreshold && indexerHeight - txBlockNumber >= blockHeightLowerThreshold);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("SwapTxFastTimer IsTxFastConfirmed Chain error, OrderId={OrderId} Message={Msg}", 
                order.Id, e.Message);
            return false;
        }
    }

    private (decimal, string, long, long, long) GetTxFastTransactionData(DepositOrderDto order)
    {
        var amount = order.ToTransfer.Amount;
        var symbol = order.ToTransfer.Symbol;
        var thresholdExists = _withdrawOptions.Value.Homogeneous.TryGetValue(symbol, out var threshold);
        AssertHelper.IsTrue(thresholdExists, "Homogeneous symbol {} not found", symbol);
        AssertHelper.NotNull(threshold, "Homogeneous threshold not fount, symbol:{}", symbol);

        var amountThreshold = threshold.AmountThreshold;
        var blockHeightUpperThreshold = threshold.BlockHeightUpperThreshold;
        var blockHeightLowerThreshold = threshold.BlockHeightLowerThreshold;

        return (amount, symbol, amountThreshold, blockHeightUpperThreshold, blockHeightLowerThreshold);
    }
}