using System.Collections.Generic;

namespace ETransferServer.Dtos.Info;

public class GetVolumeOverviewResult
{
    public VolumeOverview Volume { get; set; } = new();
}

public class VolumeOverview
{
    public string TotalAmountUsd { get; set; }
    public string Latest { get; set; }
    public List<OrderVolumeOverview> Day { get; set; } = new();
    public List<OrderVolumeOverview> Week { get; set; } = new();
    public List<OrderVolumeOverview> Month { get; set; } = new();
}

public class OrderVolumeOverview
{
    public string Date { get; set; }
    public string DepositAmountUsd { get; set; }
    public string WithdrawAmountUsd { get; set; }
    public string TransferAmountUsd { get; set; }
}