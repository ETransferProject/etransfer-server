using AElf;
using AElf.Types;
using ETransfer.Contracts.TokenPool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Swap;
using ETransferServer.Grains.Grain.Token;
using Google.Protobuf;
using NBitcoin;
using Transaction = AElf.Types.Transaction;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public partial class UserDepositGrain
{
    private readonly IContractProvider _contractProvider;

    private async Task<DepositOrderChangeDto> OnToStartTransfer(DepositOrderDto orderDto)
    {
        if (NeedSwap(orderDto))
        {
            return await ToStartSwapTx(orderDto);
        }

        return await ToStartTransfer(orderDto, true);
    }

    private async Task<DepositOrderChangeDto> ToStartTransfer(DepositOrderDto orderDto, bool withNewTx = false)
    {
        try
        {
            Transaction rawTransaction;
            var toTransfer = orderDto.ToTransfer;

            if (orderDto.FromRawTransaction.IsNullOrEmpty() || withNewTx)
            {
                var tokenGrain =
                    GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(toTransfer.Symbol, toTransfer.ChainId));
                var tokenInfo = await tokenGrain.GetToken();
                AssertHelper.NotNull(tokenInfo, "Token info {symbol}-{chainId} not found", toTransfer.Symbol,
                    toTransfer.ChainId);

                var amount = (long)(toTransfer.Amount * (decimal)Math.Pow(10, tokenInfo.Decimals));
                var releaseTokenInput = new ReleaseTokenInput
                {
                    Symbol = toTransfer.Symbol,
                    Amount = amount,
                    From = Address.FromBase58(toTransfer.FromAddress),
                    To = Address.FromBase58(toTransfer.ToAddress),
                    Memo = ITokenGrain.GetNewId(orderDto.Id)
                };
                var (txId, newTransaction) = await _contractProvider.CreateTransactionAsync(toTransfer.ChainId,
                    _chainOptions.Value.ChainInfos[toTransfer.ChainId].ReleaseAccount,
                    CommonConstant.ETransferTokenPoolContractName, CommonConstant.ETransferReleaseToken,
                    releaseTokenInput);

                toTransfer.TxId = txId.ToHex();
                rawTransaction = newTransaction;
                orderDto.FromRawTransaction = newTransaction.ToByteArray().ToHex();
            }
            else
            {
                rawTransaction =
                    Transaction.Parser.ParseFrom(ByteStringHelper.FromHexString(orderDto.FromRawTransaction));
            }

            toTransfer.TxTime = DateTime.UtcNow.ToUtcMilliSeconds();
            toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();

            orderDto.Status = OrderStatusEnum.ToTransferring.ToString();

            await AddOrUpdateOrder(orderDto, ExtensionBuilder.New()
                .Add(ExtensionKey.IsForward, Boolean.FalseString)
                .Add(ExtensionKey.TransactionId, toTransfer.TxId)
                .Add(ExtensionKey.Transaction, JsonConvert.SerializeObject(rawTransaction, JsonSettings))
                .Build());

            // send 
            var (isSuccess, error) = await _contractProvider.SendTransactionAsync(toTransfer.ChainId, rawTransaction);
            AssertHelper.IsTrue(isSuccess, error);

            var result = await _contractProvider.WaitTransactionResultAsync(toTransfer.ChainId, toTransfer.TxId,
                _chainOptions.Value.Contract.WaitSecondsAfterSend * 1000,
                _chainOptions.Value.Contract.RetryDelaySeconds * 1000);

            switch (result.Status)
            {
                case CommonConstant.TransactionState.Mined:
                    toTransfer.Status = OrderTransferStatusEnum.Transferred.ToString();
                    orderDto.Status = OrderStatusEnum.ToTransferred.ToString();
                    break;
                case CommonConstant.TransactionState.NodeValidationFailed:
                    toTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
                    orderDto.Status = OrderStatusEnum.ToTransferFailed.ToString();
                    break;
                default:
                    toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
                    orderDto.Status = OrderStatusEnum.ToTransferring.ToString();
                    break;
            }

            return new DepositOrderChangeDto
            {
                DepositOrder = orderDto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, result.Status)
                    .Add(ExtensionKey.TransactionError, result.Error)
                    .Build()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Deposit order handle error, status={Status}",
                OrderStatusEnum.ToStartTransfer.ToString());

            orderDto.ToTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
            orderDto.Status = OrderStatusEnum.ToTransferFailed.ToString();
            return new DepositOrderChangeDto
            {
                DepositOrder = orderDto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionError, e.Message)
                    .Build()
            };
        }
    }

    private async Task<DepositOrderChangeDto> ToStartSwapTx(DepositOrderDto orderDto)
    {
        _logger.LogInformation("ToStartSwapTx, orderDto: {orderDto}", JsonConvert.SerializeObject(orderDto));
        
        var swapGrain = GrainFactory.GetGrain<ISwapGrain>(orderDto.Id);
        var result = await swapGrain.Swap(orderDto);
        if (result.Success)
        {
            // orderDto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapStage, SwapStage.SwapSubsidy);
            _logger.LogInformation("ToStartSwapTx success, result: {result}", JsonConvert.SerializeObject(result));
            return result.Data;
        }

        _logger.LogInformation("ToStartSwapTx method validation or invocation failed, will call ToStartTransfer, result: {result}, order: {order}",
            JsonConvert.SerializeObject(result), JsonConvert.SerializeObject(orderDto));

        await DepositSwapFailureAlarmAsync(orderDto, SwapStage.SwapTxCheckFailAndToTransfer);
        
        orderDto.Status = OrderStatusEnum.ToStartTransfer.ToString();
        orderDto.ToTransfer.Status = OrderTransferStatusEnum.StartTransfer.ToString();
        orderDto.ToTransfer.Symbol = orderDto.FromTransfer.Symbol;
        orderDto.FromRawTransaction = null;
        orderDto.ToTransfer.TxId = null;
        orderDto.ToTransfer.TxTime = null;
        orderDto.ExtensionInfo.AddOrReplace(ExtensionKey.NeedSwap, Boolean.FalseString);
        orderDto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapStage, SwapStage.SwapTxCheckFailAndToTransfer);
        
        _logger.LogInformation("Before calling the ToStartTransfer method, after resetting the properties of the order, order: {order}",
            JsonConvert.SerializeObject(orderDto));
        
        return await ToStartTransfer(orderDto, true);
    }

    private bool NeedSwap(DepositOrderDto orderDto)
    {
        return orderDto.ExtensionInfo.ContainsKey(ExtensionKey.NeedSwap) &&
               orderDto.ExtensionInfo[ExtensionKey.NeedSwap].Equals(Boolean.TrueString);
    }

    private async Task DepositSwapFailureAlarmAsync(DepositOrderDto orderDto, string reason)
    {
        var depositSwapMonitorGrain = GrainFactory.GetGrain<IDepositSwapMonitorGrain>(orderDto.Id.ToString());
        await depositSwapMonitorGrain.DoMonitor(DepositSwapMonitorDto.Create(orderDto, reason));
    }
}