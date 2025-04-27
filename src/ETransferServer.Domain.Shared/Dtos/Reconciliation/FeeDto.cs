using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class FeeOverviewDto
{
    public FeeOverviewDetailDto Fee { get; set; } = new();
}

public class FeeChangeListDto<T> : PagedResultDto<T>
{
    public Dictionary<string, Dictionary<string, List<FeeChangeDto>>> Fee { get; set; } = new();
}

public class GetFeeRequestDto
{
    [Range(0, 2)]
    [Required]
    public int Type { get; set; }
    [Required]
    public string Symbol { get; set; }
    [Required]
    public string ResetAmount { get; set; }
}

public class FeeOverviewDetailDto
{
    public FeeItemListDto ThirdPart { get; set; } = new();
    public FeeItemListDto Etransfer { get; set; } = new();
    public FeeItemListDto Subsidy { get; set; } = new();
}

public class FeeItemListDto
{
    public string TotalUsd { get; set; }
    public List<FeeItemDto> Items { get; set; } = new();
}

public class FeeItemDto
{
    public string Symbol { get; set; }
    public string CurrentAmount { get; set; }
    public string InitAmount { get; set; }
    public string ChangeAmount { get; set; }
    public string ChangeAmountUsd { get; set; }
}

public class FeeChangeDto
{
    public string Symbol { get; set; }
    public string ChangeAmount { get; set; }
}