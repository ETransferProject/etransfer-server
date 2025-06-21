using System.Collections.Generic;

namespace ETransferServer.Options;

public class NetworkInfoOptions
{
    public string Network { get; set; }
    public string Name { get; set; }
    public string MultiConfirm { get; set; }
    public decimal MultiConfirmSeconds { get; set; }
    // for aelf chain token pool contract address
    public string TokenPoolContractAddress { get; set; }
    public string TokePoolExplorerUrl { get; set; }
    public bool IsTokenAccessRange { get; set; }
    public List<string> ExtraNotesTemplate { get; set; }
    public List<string> SwapExtraNotesTemplate { get; set; }
    public Dictionary<string, List<string>> TransferPath { get; set; } = new();
    public int ConfirmNum { get; set; }
    public decimal BlockingTime { get; set; }
    public int ExtraRequestTime { get; set; } = 30;
    public int EstimatedArrivalTime { get; set; } = 1000;
    public decimal FeeAlarmPercent { get; set; } = 10;
}