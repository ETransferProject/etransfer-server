namespace ETransferServer.Grains.Options;

public class DepositOptions
{
    public string OrderChangeTopic { get; set; }
    public string PaymentAddress { get; set; }
    public Dictionary<string, string> PaymentAddresses { get; set; } = new();
    public int ToTransferMaxRetry { get; set; } = 5;
}

