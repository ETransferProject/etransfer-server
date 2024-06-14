namespace ETransferServer.Grains.Options;

public class DepositOptions
{
    public string OrderChangeTopic { get; set; }
    public string PaymentAddress { get; set; }
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
    public int ToTransferMaxRetry { get; set; } = 5;
    public int MaxListLength { get; set; } = 1000;
    public List<string> NoSwapSymbols { get; set; } = new() {"SGR-1"};
}

