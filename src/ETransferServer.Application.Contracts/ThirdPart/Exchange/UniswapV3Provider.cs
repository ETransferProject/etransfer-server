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
    private readonly IOptionsSnapshot<ExchangeOptions> _exchangeOptions;
    
    public UniswapV3Provider(IOptionsSnapshot<ExchangeOptions> exchangeOptions,
        ILogger<UniswapV3Provider> logger)
    {
        _exchangeOptions = exchangeOptions;
        _client = new GraphQLHttpClient(_exchangeOptions.Value.UniswapV3.BaseUrl, new NewtonsoftJsonSerializer());
        _logger = logger;
    }
    
    public ExchangeProviderName Name()
    {
        return ExchangeProviderName.UniSwapV3;
    }

    private string MappingSymbol(string standardSymbol)
    {
        return _exchangeOptions.Value.UniswapV3.SymbolMapping.GetValueOrDefault(standardSymbol, standardSymbol);
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
        var poolId = _exchangeOptions.Value.UniswapV3.PoolId.GetValueOrDefault(symbolPair);
        AssertHelper.NotEmpty(poolId, "PoolId not found of {}", symbolPair);
        var resp = await _client.SendQueryAsync<ResponseWrapper<List<SwapResponse>>>(new GraphQLRequest
        {
            Query = @"query($poolId:String){
                        data:pools(
                            where: { id: $poolId }
                        ) {
                            token0{id, symbol, derivedETH}, 
                            token1{id, symbol, derivedETH}
                        }
                    }",
            Variables = new
            {
                poolId
            }
        });
        _logger.LogDebug("UniSwapV3 price pair={Pair} poolId={PoolId}, resp={Resp}", symbolPair, poolId,
            JsonConvert.SerializeObject(resp));
        AssertHelper.IsTrue(resp.Data != null, "Response data empty");
        AssertHelper.NotEmpty(resp.Data!.Data, "Response list empty");
        var swapResp = resp.Data.Data[0];
        
        var priceFrom = (swapResp.Token0.Symbol.Equals(from) ? swapResp.Token0 : swapResp.Token1).DerivedETH.SafeToDecimal();
        var priceTo = (swapResp.Token0.Symbol.Equals(to) ? swapResp.Token0 : swapResp.Token1).DerivedETH.SafeToDecimal();
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Timestamp = swapResp.Timestamp.SafeToLong() * 1000,
            Exchange = priceFrom / priceTo
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
        public string Symbol { get; set; }
        public string DerivedETH { get; set; }
        public string Decimals { get; set; }
    }
    
}