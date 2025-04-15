using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class MultiPoolOverviewDto
{
    public Dictionary<string, List<PoolOverviewDto>> MultiPool { get; set; } = new();
}

public class MultiPoolChangeListDto<T> : PagedResultDto<T>
{
    public Dictionary<string, List<MultiPoolChangeDto>> MultiPool { get; set; } = new();
}

public class GetMultiPoolRequestDto
{
    [Required]
    public string Symbol { get; set; }
    [Required]
    public string Network { get; set; }
    [Required]
    public string ResetAmount { get; set; }
}

public class PoolOverviewDto
{
    public string Symbol { get; set; }
    public string CurrentAmount { get; set; }
    public string ThresholdAmount { get; set; }
}

public class MultiPoolChangeDto
{
    public string Symbol { get; set; }
    public string Network { get; set; }
    public string ChangeAmount { get; set; }
}