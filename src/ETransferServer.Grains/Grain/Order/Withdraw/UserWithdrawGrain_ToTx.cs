using AElf;
using AElf.Types;
using ETransfer.Contracts.TokenPool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Token;
using Google.Protobuf;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public partial class UserWithdrawGrain
{
    public async Task<WithdrawOrderChangeDto> OnToStartTransfer(WithdrawOrderDto orderDto, bool withNewTx = false)
    {
        try
        {
            var newTxId = string.Empty;
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
                var paymentAddressExists =
                    _withdrawOptions.Value.PaymentAddresses?.ContainsKey(toTransfer.ChainId) ?? false;
                AssertHelper.IsTrue(paymentAddressExists, "Payment address missing, ChainId={ChainId}", toTransfer.ChainId);
                var paymentAddressDic = _withdrawOptions.Value.PaymentAddresses.GetValueOrDefault(toTransfer.ChainId);
                AssertHelper.NotEmpty(paymentAddressDic, "Payment address empty, ChainId={ChainId}", toTransfer.ChainId);
                toTransfer.FromAddress = paymentAddressDic.GetValueOrDefault(toTransfer.Symbol);
                AssertHelper.NotEmpty(toTransfer.FromAddress, "Payment address empty, Symbol={Symbol}", toTransfer.Symbol);
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
                
                newTxId = txId.ToHex();
                rawTransaction = newTransaction;
            }
            else
            {
                rawTransaction = Transaction.Parser.ParseFrom(ByteStringHelper.FromHexString(orderDto.FromRawTransaction));
            }

            toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
            orderDto.Status = OrderStatusEnum.ToTransferring.ToString();

            // check
            if (!await _orderTxFlowGrain.Check(orderDto.ToTransfer.ChainId))
            {
                _logger.LogInformation("Withdraw send transaction intercept hit: {OrderId}, {ChainId}",
                    orderDto.Id, orderDto.ToTransfer.ChainId);
                return new WithdrawOrderChangeDto
                {
                    WithdrawOrder = orderDto
                };
            }
            
            if (!newTxId.IsNullOrEmpty())
            {
                toTransfer.TxId = newTxId;
                orderDto.FromRawTransaction = rawTransaction.ToByteArray().ToHex();
            }
            toTransfer.TxTime = DateTime.UtcNow.ToUtcMilliSeconds();
            await SaveOrderTxFlowAsync(orderDto, ThirdPartOrderStatusEnum.Pending.ToString());
            await AddOrUpdateOrder(orderDto, ExtensionBuilder.New()
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

            return new WithdrawOrderChangeDto
            {
                WithdrawOrder = orderDto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, result.Status)
                    .Add(ExtensionKey.TransactionError, result.Error)
                    .Build()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Withdraw order handle error, status={Status}",
                OrderStatusEnum.ToStartTransfer.ToString());

            orderDto.ToTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
            orderDto.Status = OrderStatusEnum.ToTransferFailed.ToString();
            return new WithdrawOrderChangeDto
            {
                WithdrawOrder = orderDto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionError, e.Message)
                    .Build()
            };
        }
    }
}