using System.Collections.Generic;

namespace ETransferServer.Dtos.Info;

public class GetTransactionOverviewResult
{
    public TransactionOverview Transaction { get; set; } = new();
}

public class TransactionOverview
{
    public long TotalTx { get; set; }
    public string Latest { get; set; }
    public List<OrderTxOverview> Day { get; set; } = new();
    public List<OrderTxOverview> Week { get; set; } = new();
    public List<OrderTxOverview> Month { get; set; } = new();
}

public class OrderTxOverview
{
    public string Date { get; set; }
    public long DepositTx { get; set; }
    public long WithdrawTx { get; set; }
    public long TransferTx { get; set; }
}