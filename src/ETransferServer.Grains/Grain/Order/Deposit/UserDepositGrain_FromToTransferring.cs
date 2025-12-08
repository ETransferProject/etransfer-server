using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public partial class UserDepositGrain
{

    private async Task OnToTransferred(DepositOrderDto orderDto)
    {
        var timerTransaction = new TimerTransaction
        {
            TxId = orderDto.ToTransfer.TxId,
            TxTime = orderDto.ToTransfer.TxTime,
            ChainId = orderDto.ToTransfer.ChainId,
            TransferType = TransferTypeEnum.ToTransfer.ToString()
        };
        if (NeedSwap(orderDto))
        {
            if (IsSwapToMain(orderDto))
            {
                await _swapTxFastTimerGrain.AddToPendingList(orderDto.Id, timerTransaction);
                return;
            }

            await _swapTxTimerGrain.AddToPendingList(orderDto.Id, timerTransaction);
            return;
        }

        await _depositTxTimerGrain.AddToPendingList(orderDto.Id, timerTransaction);
    }
}