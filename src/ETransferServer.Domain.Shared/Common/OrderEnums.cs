namespace ETransferServer.Common;

public enum OrderTypeEnum
{
    Deposit,
    Withdraw,
}

public enum OrderTransferStatusEnum
{
    Initialized,
    Created,
    Pending,
    StartTransfer,
    Transferring,
    Transferred,
    TransferFailed,
    Confirmed,
    Failed,
}

public enum OrderStatusEnum
{
    Initialized,
    Created,
    Pending,

    FromStartTransfer,
    FromTransferring,
    FromTransferred,
    FromTransferConfirmed,
    FromTransferFailed,

    ToStartTransfer,
    ToTransferring,
    ToTransferred,
    ToTransferConfirmed,
    ToTransferFailed,

    Finish,
    Expired,
    Failed,
}

public enum ThirdPartOrderStatusEnum
{
    Success,
    Pending,
    Fail
}