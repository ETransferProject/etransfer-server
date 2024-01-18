using System.Collections.Generic;

namespace ETransferServer.Deposit.Dtos;

public class GetDepositInfoDto
{
    public DepositInfoDto DepositInfo { get; set; }
}

public class DepositInfoDto
{
    public string DepositAddress { get; set; }
    public string MinAmount { get; set; }
    public List<string> ExtraNotes { get; set; }
}