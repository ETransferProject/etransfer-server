using System.Collections.Generic;

namespace ETransferServer.Dtos.Info;

public class GetTokenResultDto : Dictionary<string, TokenResultDto>
{
}

public static class DictionaryExtensions
{
    public static GetTokenResultDto ToDictionary(this Dictionary<string, TokenResultDto> dictionary)
    {
        var dic = new GetTokenResultDto();
        foreach (var kvp in dictionary)
        {
            dic.Add(kvp.Key, kvp.Value);
        }
        return dic;
    }
}

public class TokenResultDto
{
    public string Icon { get; set; }
    public List<string> Networks { get; set; } = new();
    public List<string> ChainIds { get; set; } = new();
    public AmountDto General { get; set; } = new();
    public List<DetailDto> Details { get; set; } = new();
}

public class AmountDto
{
    public string Amount24H { get; set; } = "0";
    public string Amount24HUsd { get; set; } = "0";
    public string Amount7D { get; set; } = "0";
    public string Amount7DUsd { get; set; } = "0";
    public string AmountTotal { get; set; } = "0";
    public string AmountTotalUsd { get; set; } = "0";
}

public class DetailDto
{
    public string Name { get; set; }
    public AmountDto Item { get; set; } = new();
}