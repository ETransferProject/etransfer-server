using ETransferServer.Common;

namespace ETransferServer.Grains.Common;

public static class OrderIdHelper
{

    public static Guid DepositOrderId(string network, string symbol, string depositTxId)
    {
        return GuidHelper.UniqGuid(network, symbol, depositTxId);
    }
    
    public static Guid WithdrawOrderId(string id, string chainId, string address)
    {
        return GuidHelper.UniqGuid(id, chainId, address);
    }
    
}