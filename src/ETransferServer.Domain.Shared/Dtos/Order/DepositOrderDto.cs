using System;
using System.Collections.Generic;
using ETransferServer.Common;

namespace ETransferServer.Dtos.Order;

public class DepositOrderDto : BaseOrderDto
{
    public string? FromRawTransaction { get; set; }
}

public class DepositOrderChangeDto
{
    public DepositOrderDto DepositOrder { get; set; }
    public Dictionary<string, string> ExtensionData { get; set; }
}

public class DepositRequest
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    
    public string ThirdPartOrderId { get; set; }
    
    /// <see cref="ThirdPartServiceNameEnum"/>
    public string ThirdPartServiceName { get; set; }
    
    public TransferInfo FromTransfer { get; set; }
    
    public TransferInfo ToTransfer { get; set; }
}