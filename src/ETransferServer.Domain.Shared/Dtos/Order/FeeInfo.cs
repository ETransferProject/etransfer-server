namespace ETransferServer.Dtos.Order;

public class FeeInfo
{
    public string? Name { get; set; }
    public string Symbol { get; set; }
    public string Amount { get; set; }
    public string Decimals { get; set; }


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