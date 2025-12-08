using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class TokenPoolOverviewDto
{
    public Dictionary<string, List<PoolOverviewDto>> TokenPool { get; set; } = new();
}

public class TokenPoolChangeListDto<T> : PagedResultDto<T>
{
    public Dictionary<string, List<TokenPoolChangeDto>> TokenPool { get; set; } = new();
}

public class GetTokenPoolRequestDto
{
    [Required]
    public string Symbol { get; set; }
    [Required]
    public string ChainId { get; set; }
    [Required]
    public string ResetAmount { get; set; }
}

public class TokenPoolChangeDto
{
    public string Symbol { get; set; }
    public string ChainId { get; set; }
    public string ChangeAmount { get; set; }
}