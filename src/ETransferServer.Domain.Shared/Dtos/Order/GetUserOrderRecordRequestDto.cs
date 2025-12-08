using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class GetUserOrderRecordRequestDto
{
    public string? Address { get; set; }
    public List<GetUserAddressDto>? AddressList { get; set; }
    public long? Time { get; set; }
}

public class GetUserAddressDto{
    public string SourceType { get; set; }
    public string Address { get; set; }
}