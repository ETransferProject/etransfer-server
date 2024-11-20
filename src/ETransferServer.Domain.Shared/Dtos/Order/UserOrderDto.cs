using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class UserOrderDto
{
    public string Address { get; set; }
    public UserOrderRecordDto Processing { get; set; } = new();
    public UserOrderRecordDto Succeed { get; set; } = new();
    public UserOrderRecordDto Failed { get; set; } = new();
}

public class UserOrderRecordDto
{
    public int DepositCount { get; set; }
    public int WithdrawCount { get; set; }
    public int TransferCount { get; set; }
    public List<UserDepositOrderInfo> Deposit { get; set; }
    public List<UserTransferOrderInfo> Withdraw { get; set; }
    public List<UserTransferOrderInfo> Transfer { get; set; }
}

public class UserOrderRecordInfo
{
    public string Id { get; set; }
    public string Amount { get; set; }
    public string Symbol { get; set; }
}

public class UserDepositOrderInfo : UserOrderRecordInfo
{
    public bool IsSwap { get; set; }
    public bool IsSwapFail { get; set; }
}

public class UserTransferOrderInfo : UserOrderRecordInfo
{
}