namespace ETransferServer.Dtos.Order;

public class OrderDetailDto : OrderIndexDto
{
    public StepInfoDto Step { get; set; } = new();
}

public class StepInfoDto
{
    public int CurrentStep { get; set; }
    public StepTransferInfoDto FromTransfer { get; set; } = new();
}

public class StepTransferInfoDto
{
    public int ConfirmingThreshold { get; set; }
    public int ConfirmedNum { get; set; }
}