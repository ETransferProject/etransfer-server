using System.Collections.Generic;
using JetBrains.Annotations;

namespace ETransferServer.Network.Dtos;

public class TransactionNotificationResponse
{
    public bool Success { get; set; }
    public string Result { get; set; }
    public string Exception { get; set; }
}

