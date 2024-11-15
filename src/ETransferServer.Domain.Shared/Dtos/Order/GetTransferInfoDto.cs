namespace ETransferServer.Withdraw.Dtos;

public class GetTransferInfoDto
{
    public TransferDetailInfoDto TransferInfo { get; set; }
}

public class TransferDetailInfoDto : WithdrawInfoDto
{
    public string ContractAddress { get; set; }
}