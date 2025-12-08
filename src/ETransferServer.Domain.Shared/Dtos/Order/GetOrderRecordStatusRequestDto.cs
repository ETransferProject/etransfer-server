using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class GetOrderRecordStatusRequestDto
{
    public List<string>? AddressList { get; set; }
}