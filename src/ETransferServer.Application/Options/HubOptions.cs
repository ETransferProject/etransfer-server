namespace ETransferServer.Options;

public class HubOptions
{
    public int ExpireDays { get; set; } = 1;
    public int HubLimit { get; set; } = 100;
}