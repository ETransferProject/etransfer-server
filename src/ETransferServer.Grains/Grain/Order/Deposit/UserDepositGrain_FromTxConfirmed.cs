using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Token;
using Google.Protobuf;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public partial class UserDepositGrain
{
    private readonly IContractProvider _contractProvider;

    private async Task<DepositOrderChangeDto> OnToStartTransfer(DepositOrderDto orderDto)
    {
        if (IsSwapTx(orderDto))
        {
            // raymond.zhang
            // return await ToStartSwapTx(orderDto);
            // if (fail)
            // {
            //     return await ToStartTransfer(orderDto);
            // }

        } else if (IsSwapSubsidy(orderDto))
        {
            // raymond.zhang
            // return await ToStartSwapSubsidy(orderDto);
        }  
        
        return await ToStartTransfer(orderDto);
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
                var transferInput = new TransferInput
                {
                    To = Address.FromBase58(toTransfer.ToAddress),
                    Amount = amount,
                    Symbol = toTransfer.Symbol,
                    Memo = ITokenGrain.GetNewId(orderDto.Id)
                };
                var (txId, newTransaction) = await _contractProvider.CreateTransactionAsync(toTransfer.ChainId, toTransfer.FromAddress,
                    SystemContractName.TokenContract, "Transfer", transferInput);

                toTransfer.TxId = txId.ToHex();
                rawTransaction = newTransaction;
                orderDto.FromRawTransaction = newTransaction.ToByteArray().ToHex();
            }
            else
            {
                rawTransaction = Transaction.Parser.ParseFrom(ByteStringHelper.FromHexString(orderDto.FromRawTransaction));
            }

            toTransfer.TxTime = DateTime.UtcNow.ToUtcMilliSeconds();
            toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();

            orderDto.Status = OrderStatusEnum.ToTransferring.ToString();

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
        // raymond.zhang
        // Task<GrainResultDto<DepositOrderChangeDto>> SwapAsync(DepositOrderDto dto);
        return null;
    }
    
    private async Task<DepositOrderChangeDto> ToStartSwapSubsidy(DepositOrderDto orderDto)
    {
        // raymond.zhang
        // Task<GrainResultDto<DepositOrderChangeDto>> SubsidyTransferAsync(DepositOrderDto dto，string returnValue);
        return null;
    }

    private bool NeedSwap(DepositOrderDto orderDto)
    {
        return orderDto.ExtensionInfo.ContainsKey(ExtensionKey.NeedSwap) &&
               orderDto.ExtensionInfo[ExtensionKey.NeedSwap].Equals(Boolean.TrueString);
    }
    
    private bool IsSwapTx(DepositOrderDto orderDto)
    {
        if (NeedSwap(orderDto))
        {
            return orderDto.ExtensionInfo.ContainsKey(ExtensionKey.SwapStage) &&
                   orderDto.ExtensionInfo[ExtensionKey.SwapStage].Equals(SwapStage.SwapTx);
        }
        return false;
    }
    
    private bool IsSwapSubsidy(DepositOrderDto orderDto)
    {
        if (NeedSwap(orderDto))
        {
            return orderDto.ExtensionInfo.ContainsKey(ExtensionKey.SwapStage) &&
                   orderDto.ExtensionInfo[ExtensionKey.SwapStage].Equals(SwapStage.SwapSubsidy);
        }
        return false;
    }
}