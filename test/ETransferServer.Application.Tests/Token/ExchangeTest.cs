using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Options;
using ETransferServer.ThirdPart.Exchange;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Orleans;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Token;

public class ExchangeTest : ETransferServerApplicationTestBase
{
    
    public ExchangeTest(ITestOutputHelper output) : base(output)
    {
        
    }


    public IOptionsMonitor<ExchangeOptions> MockExchangeOptions()
    {
        var options = new ExchangeOptions
        {
            Okx = new OkxOptions
            {
                BaseUrl = "https://aws.okx.com"
            },
            Binance = new BinanceOptions
            {
                BaseUrl = "https://data-api.binance.vision"
            },
            UniswapV3 = new UniswapV3Options
            {
                BaseUrl = "https://api.thegraph.com/subgraphs/name/uniswap/uniswap-v3",
                PoolId = new Dictionary<string, string>
                {
                    ["USDC_USDT"] = "0x7858e59e0c01ea06df3af3d20ac7b0003275d4bf"
                }
            },
            GateIo = new GateIoOptions
            {
                BaseUrl = "https://api.gateio.ws",
                
            }
        };
        var mock = new Mock<IOptionsMonitor<ExchangeOptions>>();
        mock.Setup(options => options.CurrentValue).Returns(options);
        return mock.Object;
    }
    
    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(MockExchangeOptions());
        base.AfterAddApplication(services);
    }

    

    [Fact]
    public async Task QueryExchange()
    {
        // var client = ServiceProvider.GetRequiredService<IClusterClient>();
        // var exchangeGrain = client.GetGrain<ITokenExchangeGrain>(ITokenExchangeGrain.GetGrainId("ETH", "USDT"));
        // var exchange = await exchangeGrain.GetAsync();
        var gateIoProvider = ServiceProvider.GetRequiredService<GateIoProvider>();
        var gateIoExchange = await gateIoProvider.LatestAsync("ELF", "USDT");
        Output.WriteLine(JsonConvert.SerializeObject(gateIoExchange));


        var uniswapV3Provider = ServiceProvider.GetRequiredService<UniswapV3Provider>();
        var uniswapExchange = await uniswapV3Provider.LatestAsync("USDC", "USDT");
        Output.WriteLine(JsonConvert.SerializeObject(uniswapExchange));



    }
    
    
    
    
    
    
    
}