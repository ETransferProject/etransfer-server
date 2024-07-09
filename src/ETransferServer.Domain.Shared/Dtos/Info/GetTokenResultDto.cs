using System.Collections.Generic;

namespace ETransferServer.Dtos.Info;

public class GetTokenResultDto : Dictionary<string, TokenResultDto>
{
}

public class TokenResultDto
{
    public string Icon { get; set; }
    public List<string> Networks { get; set; } = new();
    public List<string> ChainIds { get; set; } = new();
    public AmountDto General { get; set; }
    public List<DetailDto> Details { get; set; } = new();
}

public class AmountDto
{
    public string Amount24H { get; set; }
    public string Amount24HUsd { get; set; }
    public string Amount7D { get; set; }
    public string Amount7DUsd { get; set; }
    public string AmountTotal { get; set; }
    public string AmountTotalUsd { get; set; }
}

public class DetailDto
{
    public string Name { get; set; }
    public AmountDto Item { get; set; }
}