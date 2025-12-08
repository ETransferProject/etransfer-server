using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class PoolOverviewListDto
{
    public List<PoolOverviewDetailDto> Pool { get; set; } = new();
    public PoolTotalDto Total { get; set; } = new();
}

public class PoolChangeListDto<T> : PagedResultDto<T>
{
    public Dictionary<string, List<PoolChangeDto>> Pool { get; set; } = new();
}

public class GetPoolRequestDto
{
    [Required]
    public string Symbol { get; set; }
    [Required]
    public string ResetAmount { get; set; }
}

public class PoolOverviewDetailDto
{
    public string Symbol { get; set; }
    public string CurrentAmount { get; set; }
    public string CurrentAmountUsd { get; set; }
    public string InitAmount { get; set; }
    public string InitAmountUsd { get; set; }
    public string ChangeAmount { get; set; }
    public string ChangeAmountUsd { get; set; }
}

public class PoolTotalDto
{
    public string CurrentTotalAmountUsd { get; set; }
    public string InitTotalAmountUsd { get; set; }
    public string ChangeTotalAmountUsd { get; set; }
}

public class PoolChangeDto
{
    public string Symbol { get; set; }
    public string ChangeAmount { get; set; }
}