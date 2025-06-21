using System.Collections.Generic;

namespace ETransferServer.Options;

public class DepositInfoOptions
{
    public string OrderChangeTopic { get; set; }
    public int ToTransferMaxRetry { get; set; } = 5;
    public int MaxListLength { get; set; } = 1000;
    public List<string> NoSwapSymbols { get; set; } = new() {"SGR-1"};
    public Dictionary<string, Dictionary<string, List<string>>> AlarmWhiteLists { get; set; } = new();
}