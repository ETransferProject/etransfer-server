using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class FeeInfo
{
    [Id(0)] public string? Name { get; set; }
    [Id(1)] public string Symbol { get; set; }
    [Id(2)] public string Amount { get; set; }
    [Id(3)] public string Decimals { get; set; }


    public FeeInfo()
    {
        
    }
    
    public FeeInfo(string symbol, string amount, string? name = null)
    {
        Symbol = symbol;
        Amount = amount;
        Name = name;
    }
    
    public FeeInfo(string symbol, string amount, string decimals, string? name = null)
    {
        Symbol = symbol;
        Amount = amount;
        Decimals = decimals;
        Name = name;
    }
    
    public class FeeName
    {
        public const string NetworkFee = "NetworkFee";
        public const string CoBoFee = "CoBoFee";
    }
    
}