using System.Collections.Generic;

namespace ETransferServer.Common;

public static class OrderStatusHelper
{
    public static List<string> GetProcessingList()
    {
        return new List<string>
        {
            OrderStatusEnum.Initialized.ToString(),
            OrderStatusEnum.Created.ToString(),
            OrderStatusEnum.Pending.ToString(),
            OrderStatusEnum.FromStartTransfer.ToString(),
            OrderStatusEnum.FromTransferring.ToString(),
            OrderStatusEnum.FromTransferred.ToString(),
            OrderStatusEnum.FromTransferConfirmed.ToString(),
            OrderStatusEnum.ToStartTransfer.ToString(),
            OrderStatusEnum.ToTransferring.ToString(),
            OrderStatusEnum.ToTransferred.ToString()
        };
    }

    public static List<string> GetSucceedList()
    {
        return new List<string>
        {
            OrderStatusEnum.ToTransferConfirmed.ToString(),
            OrderStatusEnum.Finish.ToString()
        };
    }
    
    public static List<string> GetFailedList()
    {
        return new List<string>
        {
            OrderStatusEnum.FromTransferFailed.ToString(),
            OrderStatusEnum.ToTransferFailed.ToString(),
            OrderStatusEnum.Expired.ToString(),
            OrderStatusEnum.Failed.ToString()
        };
    }
}