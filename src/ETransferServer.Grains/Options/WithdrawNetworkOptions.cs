using ETransferServer.Common;

namespace ETransferServer.Grains.Options;

public class WithdrawNetworkOptions
{
    public List<NetWorkInfo> NetworkInfos { get; set; } = new();
    
    public NetWorkInfo GetNetworkInfo(string network, string symbol)
    {
        var coin = GuidHelper.GenerateId(network, symbol);
        var coinInfo = NetworkInfos.FirstOrDefault(t =>
            t.Coin.Equals(coin, StringComparison.OrdinalIgnoreCase));
        AssertHelper.NotNull(coinInfo, "withdraw coin not support, coin:{Coin}", coin);
        return coinInfo;
    }
    
}

public class NetWorkInfo
{
    // network_symbol
    public string Coin { get; set; }
    public decimal Amount { get; set; } = 0;
}