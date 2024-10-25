using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public partial class UserDepositGrain
{

    private async Task OnToTransferred(DepositOrderDto orderDto)
    {
        if (NeedSwap(orderDto))
        {
            await _swapTxTimerGrain.AddToPendingList(orderDto.Id, new TimerTransaction
            {
                TxId = orderDto.ToTransfer.TxId,
                TxTime = orderDto.ToTransfer.TxTime,
                ChainId = orderDto.ToTransfer.ChainId,
                TransferType = TransferTypeEnum.ToTransfer.ToString()
            });
            return ;
        }

        await _depositTxTimerGrain.AddToPendingList(orderDto.Id, new TimerTransaction
        {
            TxId = orderDto.ToTransfer.TxId,
            TxTime = orderDto.ToTransfer.TxTime,
            ChainId = orderDto.ToTransfer.ChainId,
            TransferType = TransferTypeEnum.ToTransfer.ToString()
        });
    }

    
}