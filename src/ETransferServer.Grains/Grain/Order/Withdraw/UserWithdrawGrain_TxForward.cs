using AElf;
using AElf.Types;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public partial class UserWithdrawGrain
{

    public async Task<WithdrawOrderDto> TransferForward(WithdrawOrderDto orderDto)
    {
        // verify transaction signature
        var transaction =
            Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(orderDto.RawTransaction));

        var txId = transaction.GetHash().ToHex();

        // save order data 
        orderDto.FromTransfer.TxId = txId;
        orderDto.FromTransfer.TxTime = DateTime.UtcNow.ToUtcMilliSeconds();
        orderDto.FromTransfer.Status = OrderTransferStatusEnum.StartTransfer.ToString();
        orderDto.Status = OrderStatusEnum.FromTransferring.ToString();

        await AddOrUpdateOrder(orderDto, ExtensionBuilder.New()
            .Add(ExtensionKey.TransactionId, txId)
            .Add(ExtensionKey.Transaction, JsonConvert.SerializeObject(transaction))
            .Build());

        try
        {
            // send transaction to node
            var (sendSuccess, sendError) =
                await _contractProvider.SendTransactionAsync(orderDto.FromTransfer.ChainId, transaction);
            AssertHelper.IsTrue(sendSuccess, "Transaction send failed {Error}", sendError);

            var result = await _contractProvider.WaitTransactionResultAsync(orderDto.FromTransfer.ChainId,
                orderDto.FromTransfer.TxId,
                _chainOptions.CurrentValue.Contract.WaitSecondsAfterSend * 1000,
                _chainOptions.CurrentValue.Contract.RetryDelaySeconds * 1000);

            switch (result.Status)
            {
                case CommonConstant.TransactionState.Mined:
                    orderDto.FromTransfer.Status = OrderTransferStatusEnum.Transferred.ToString();
                    orderDto.Status = OrderStatusEnum.FromTransferred.ToString();
                    break;
                case CommonConstant.TransactionState.NodeValidationFailed:
                    orderDto.FromTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
                    orderDto.Status = OrderStatusEnum.FromTransferFailed.ToString();
                    break;
                default:
                    orderDto.FromTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
                    orderDto.Status = OrderStatusEnum.FromTransferring.ToString();
                    break;
            }

            return await AddOrUpdateOrder(orderDto);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Withdraw FromTransfer error, orderId={OrderId}", orderDto.Id);
            orderDto.FromTransfer.Status = OrderTransferStatusEnum.TransferFailed.ToString();
            orderDto.Status = OrderStatusEnum.FromTransferFailed.ToString();
            return await AddOrUpdateOrder(orderDto, ExtensionBuilder.New()
                .Add(ExtensionKey.TransactionError, e.Message)
                .Build());
        }
    }

}