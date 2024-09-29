using System.Collections.Generic;
using ETransferServer.Dtos.Order;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class OrderRecordDto : OrderIndexDto
{
    public int RoleType { get; set; } = -1;
    public string OperationStatus { get; set; }
    public string Applicant { get; set; }
}

public class OrderPagedResultDto<T> : PagedResultDto<T>
{
    public Dictionary<string, string> TotalAmount { get; set; }
}