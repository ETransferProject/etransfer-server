using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Options;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ThirdPart.Exchange;

public class UniswapV3Provider : IExchangeProvider, ISingletonDependency
{

    private readonly ILogger<UniswapV3Provider> _logger;
    private readonly GraphQLHttpClient _client;
    private readonly IOptionsMonitor<ExchangeOptions> _exchangeOptions;
    
    public UniswapV3Provider(IOptionsMonitor<ExchangeOptions> exchangeOptions,
        ILogger<UniswapV3Provider> logger)
    {
        _exchangeOptions = exchangeOptions;
        _client = new GraphQLHttpClient(_exchangeOptions.CurrentValue.UniswapV3.BaseUrl, new NewtonsoftJsonSerializer());
        _logger = logger;
    }
    
    public ExchangeProviderName Name()
    {
        return ExchangeProviderName.UniSwapV3;
    }

    private string MappingSymbol(string standardSymbol)
    {
        return _exchangeOptions.CurrentValue.UniswapV3.SymbolMapping.GetValueOrDefault(standardSymbol, standardSymbol);
    }

    public async Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol)
    {
        var from = MappingSymbol(fromSymbol);
        var to = MappingSymbol(toSymbol);
        if (from == to)
        {
            return TokenExchangeDto.One(fromSymbol, toSymbol, DateTime.UtcNow.ToUtcMilliSeconds());
        }

        var symbolPair = string.Join(CommonConstant.Underline, MappingSymbol(fromSymbol), MappingSymbol(toSymbol));
        var poolId = _exchangeOptions.CurrentValue.UniswapV3.PoolId.GetValueOrDefault(symbolPair);
        AssertHelper.NotEmpty(poolId, "PoolId not found of {}", symbolPair);
        var resp = await _client.SendQueryAsync<ResponseWrapper<List<SwapResponse>>>(new GraphQLRequest
        {
            Query = @"query($poolId:String){
                        data:swaps(
                            orderBy: timestamp, orderDirection:desc, first: 1,
                            where: { pool: $poolId }
                        ) {
                            id, timestamp, 
                            token0{id, name, derivedETH}, 
                            token1{id, name, derivedETH}
                        }
                    }",
            Variables = new
            {
                poolId
            }
        });
        _logger.LogDebug("UniSwapV3 price pair={Pair} poolId={poolId}, resp={Resp}", symbolPair, poolId,
            JsonConvert.SerializeObject(resp));
        AssertHelper.IsTrue(resp.Data != null, "Response data empty");
        AssertHelper.NotEmpty(resp.Data!.Data, "Response list empty");
        var swapResp = resp.Data.Data[0];
        var price0InEth = swapResp.Token0.DerivedETH.SafeToDecimal();
        var price1InEth = swapResp.Token1.DerivedETH.SafeToDecimal();
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Timestamp = swapResp.Timestamp.SafeToLong() * 1000,
            Exchange = price0InEth / price1InEth
        };
    }

    public Task<List<TokenExchangeDto>> LatestAsync(List<string> fromSymbol, string toSymbol)
    {
        throw new System.NotImplementedException();
    }

    public Task<TokenExchangeDto> HistoryAsync(string fromSymbol, string toSymbol, long timestamp)
    {
        throw new System.NotImplementedException();
    }

    public class BaseResponse<T>
    {
        public ResponseWrapper<T> Data { get; set; }

        public T GetData()
        {
            return Data.Data;
        }
    }

    public class ResponseWrapper<T>
    {
        public T Data { get; set; }
    }
    
    public class SwapResponse
    {
        public string Id { get; set; }
        public string Timestamp { get; set; }
        public SwapToken Token0 { get; set; }
        public SwapToken Token1 { get; set; }
    }

    public class SwapToken
    {
        public string Id { get; set; }
        public string DerivedETH { get; set; }
        public string Decimals { get; set; }
    }
    
}