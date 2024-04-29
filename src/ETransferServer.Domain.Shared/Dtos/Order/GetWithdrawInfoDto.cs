using Newtonsoft.Json;

namespace ETransferServer.Withdraw.Dtos;

public class GetWithdrawInfoDto
{
    public WithdrawInfoDto WithdrawInfo { get; set; }
}

public class WithdrawInfoDto
{
    public string MaxAmount { get; set; }
    public string MinAmount { get; set; }
    public string LimitCurrency { get; set; }
    public string TotalLimit { get; set; }
    public string RemainingLimit { get; set; }
    public string TransactionFee { get; set; }
    public string TransactionUnit { get; set; }
    public string ReceiveAmount { get; set; }
    public string AelfTransactionFee { get; set; }
    public string AelfTransactionUnit { get; set; }
    public string ExpiredTimestamp { get; set; }
    public string AmountUsd { get; set; }
    public string ReceiveAmountUsd { get; set; }
    public string FeeUsd { get; set; }
}