using System.Collections.Generic;

namespace ETransferServer.Options;

public class NetworkInfoOptions
{
    public Dictionary<string, List<string>> NetworkPattern { get; set; }
    public Dictionary<string,NetworkBasicInfo> Networks { get; set; } = new();
    public decimal WithdrawLimit24H { get; set; }= 50_0000;
    
    public List<string> ExtraNotesTemplate { get; set; }
    public List<string> SwapExtraNotesTemplate { get; set; }

}

public class NetworkBasicInfo
{
    public string Network { get; set; }
    public string Name { get; set; }
    public decimal MultiConfirmSeconds { get; set; }
    // for aelf chain token pool contract address
    public string TokenPoolContractAddress { get; set; }
    public string TokePoolExplorerUrl { get; set; }
    public bool IsTokenAccessRange { get; set; }

    public int ConfirmNum { get; set; }
    public decimal BlockingTime { get; set; }
    public int ExtraRequestTime { get; set; } = 30;
    public int EstimatedArrivalTime { get; set; } = 1000;
    public decimal FeeAlarmPercent { get; set; } = 10;
    public string MinShowVersion { get; set; }
    public decimal WithdrawLocalFee { get; set; }
    public string WithdrawLocalFeeUnit { get; set; }
    public string SpecialWithdrawFee { get; set; }
    public bool SpecialWithdrawFeeDisplay { get; set; } = false;
}