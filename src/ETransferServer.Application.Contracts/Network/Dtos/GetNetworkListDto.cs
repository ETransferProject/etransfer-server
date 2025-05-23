using System.Collections.Generic;
using JetBrains.Annotations;

namespace ETransferServer.Network.Dtos;

public class GetNetworkListDto
{
    public List<NetworkDto> NetworkList { get; set; }
    public string ChainId { get; set; }
}

public class NetworkDto
{
    public string Network { get; set; }
    public string Name { get; set; }
    public string MultiConfirm { get; set; }
    public string MultiConfirmTime { get; set; }
    public string ContractAddress { get; set; }
    public string ExplorerUrl { get; set; }
    public string Status { get; set; }
    public Dictionary<string, string> MultiStatus { get; set; }
    [CanBeNull] public string WithdrawFee { get; set; }
    [CanBeNull] public string WithdrawFeeUnit { get; set; }
    public bool SpecialWithdrawFeeDisplay { get; set; }
    public string SpecialWithdrawFee { get; set; }
    
}