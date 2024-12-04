using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.TokenAccess;

public class TokenApplyOrderListDto : PagedResultDto<TokenApplyOrderDto>
{
}

public class TokenApplyOrderDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; }
    public string UserAddress { get; set; }
    public string Status { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
    public List<ChainTokenInfoDto> ChainTokenInfo { get; set; } = new();
    public ChainTokenInfoDto OtherChainTokenInfo { get; set; } = new();
}

public class ChainTokenInfoDto
{
    public string ChainId { get; set; }
    public string TokenName { get; set; }
    public string Symbol { get; set; }
    public decimal TotalSupply { get; set; }
    public int Decimals { get; set; }
    public string Icon { get; set; }
    public string PoolAddress { get; set; }
}