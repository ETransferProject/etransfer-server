using System;
using System.Collections.Generic;
using ETransferServer.Common;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class DepositOrderDto : BaseOrderDto
{
    [Id(0)] public string? FromRawTransaction { get; set; }
}

[GenerateSerializer]
public class DepositOrderChangeDto
{
    [Id(0)] public DepositOrderDto DepositOrder { get; set; }
    [Id(1)] public Dictionary<string, string> ExtensionData { get; set; }
}

[GenerateSerializer]
public class DepositRequest
{
    [Id(0)] public Guid Id { get; set; }
    
    [Id(1)] public Guid UserId { get; set; }
    
    [Id(2)] public string ThirdPartOrderId { get; set; }
    
    /// <see cref="ThirdPartServiceNameEnum"/>
    [Id(3)] public string ThirdPartServiceName { get; set; }
    
    [Id(4)] public TransferInfo FromTransfer { get; set; }
    
    [Id(5)] public TransferInfo ToTransfer { get; set; }
}