using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ThirdPart.CoBo;

public class CoBoProvider : ICoBoProvider, ISingletonDependency
{
    private readonly IProxyCoBoClientProvider _proxyCoBoClientProvider;
    private readonly ILogger<CoBoProvider> _logger;
    private readonly IOptionsSnapshot<NetWorkReflectionOptions> _options;

    public CoBoProvider(IProxyCoBoClientProvider proxyCoBoClientProvider, ILogger<CoBoProvider> logger,
        IOptionsSnapshot<NetWorkReflectionOptions> options)
    {
        _proxyCoBoClientProvider = proxyCoBoClientProvider;
        _logger = logger;
        _options = options;
    }

    public async Task<CoBoCoinDetailDto> GetCoinDetailAsync(string coin)
    {
        coin = GetRequestCoin(coin);

        var result =
            await _proxyCoBoClientProvider.GetAsync<CoBoCoinDetailDto>(CoBoConstant.GetCoinDetail + "?coin=" + coin);
        _logger.LogInformation("get coin detail coin={Coin}, feeCoin={FeeCoin}, absFeeAmount={FeeAmount}", coin,
            result.FeeCoin, result.AbsEstimateFee);
        result.Coin = GetResponseCoin(result.Coin);
        return result;
    }

    public async Task<AccountDetailDto> GetAccountDetailAsync()
    {
        _logger.LogInformation("get account detail info from cobo.");
        var result = await _proxyCoBoClientProvider.GetAsync<AccountDetailDto>(CoBoConstant.GetAccountDetail);
        result?.Assets?.ForEach(t => { t.Coin = GetResponseCoin(t.Coin); });

        return result;
    }

    public async Task<AddressesDto> GetAddressesAsync(string coin, int count, bool nativeSegwit = false)
    {
        coin = GetRequestCoin(coin);
        _logger.LogInformation("get addresses info from cobo, coin:{coin}, count:{count}", coin, count);
        var dicParam = new Dictionary<string, string>
        {
            ["coin"] = coin,
            ["count"] = count.ToString()
        };

        var result = await _proxyCoBoClientProvider.PostAsync<AddressesDto>(CoBoConstant.GetAddresses, dicParam);
        result.Coin = GetResponseCoin(result.Coin);
        return result;
    }

    public async Task<List<CoBoTransactionDto>> GetTransactionsByTimeExAsync(TransactionRequestDto input)
    {
        _logger.LogInformation("get transaction by time from cobo, beginTime:{beginTime}, endTime:{endTime}",
            input.BeginTime, input.EndTime);

        var uriParam = ObjToUriParam(input);
        var result = await _proxyCoBoClientProvider.GetAsync<List<CoBoTransactionDto>>(
            CoBoConstant.GetTransactionsByTimeEx + uriParam);

        _logger.LogInformation(
            "get transaction by time from cobo, beginTime:{beginTime}, endTime:{endTime}, transaction count:{count}",
            input.BeginTime, input.EndTime, result?.Count);

        foreach (var coBoTransaction in result)
        {
            coBoTransaction.Coin = GetResponseCoin(coBoTransaction.Coin);
        }

        return result;
    }

    public async Task<CoBoTransactionDto> GetTransactionAsync(string id)
    {
        _logger.LogInformation("get transaction by id from cobo, id:{id}", id);
        var url = $"{CoBoConstant.GetTransaction}?id={id}";
        var result = await _proxyCoBoClientProvider.GetAsync<CoBoTransactionDto>(url);
        result.Coin = GetResponseCoin(result.Coin);
        return result;
    }

    public async Task<string> WithdrawAsync(WithdrawRequestDto input)
    {
        input.Coin = GetRequestCoin(input.Coin);
        _logger.LogInformation(
            "send withdraw request to cobo, requestId:{requestId}, coin:{coin}, address:{address}, amount:{amount}, memo:{memo}",
            input.RequestId, input.Coin, input.Address, input.Amount, input.Memo);
        var dicParam = ObjToDicParam(input);
        return await _proxyCoBoClientProvider.PostAsync<string>(CoBoConstant.Withdraw, dicParam);
    }

    public async Task<WithdrawInfoDto> GetWithdrawInfoByRequestIdAsync(string requestId)
    {
        _logger.LogInformation("get withdraw info by requestId from cobo, requestId:{requestId}",
            requestId);

        var url = $"{CoBoConstant.WithdrawInfoByRequestId}?request_id={requestId}";
        var result = await _proxyCoBoClientProvider.GetAsync<WithdrawInfoDto>(url);
        result.Coin = GetResponseCoin(result.Coin);
        return result;
    }

    public static string ObjToUriParam(object obj)
    {
        var properties = obj.GetType().GetProperties();
        var builder = new StringBuilder("?");
        foreach (var propertyInfo in properties)
        {
            var value = propertyInfo.GetValue(obj, null);
            if (value == null)
            {
                continue;
            }

            if (propertyInfo.IsDefined(typeof(JsonPropertyAttribute), false))
            {
                var attributeInfo = propertyInfo.GetCustomAttribute<JsonPropertyAttribute>();
                builder.Append($"{attributeInfo.PropertyName.ToLower()}={value}&");
                continue;
            }

            builder.Append($"{propertyInfo.Name.ToLower()}={value}&");
        }

        builder.Remove(builder.Length - 1, 1);
        return builder.ToString();
    }


    public static Dictionary<string, string> ObjToDicParam(object obj)
    {
        var properties = obj.GetType().GetProperties();
        var dicParam = new Dictionary<string, string>();
        foreach (var propertyInfo in properties)
        {
            var value = propertyInfo.GetValue(obj, null);
            if (value == null)
            {
                continue;
            }

            if (propertyInfo.IsDefined(typeof(JsonPropertyAttribute), false))
            {
                var attributeInfo = propertyInfo.GetCustomAttribute<JsonPropertyAttribute>();
                dicParam.Add(attributeInfo.PropertyName, value.ToString());
                continue;
            }

            dicParam.Add(propertyInfo.Name.ToLower(), value.ToString());
        }

        return dicParam;
    }

    private string GetRequestCoin(string coin)
    {
        if (_options == null || _options.Value.ReflectionItems.IsNullOrEmpty())
        {
            return coin;
        }

        return _options.Value.ReflectionItems.ContainsKey(coin) ? _options.Value.ReflectionItems[coin] : coin;
    }

    public string GetResponseCoin(string coin)
    {
        if (_options == null || _options.Value.ReflectionItems.IsNullOrEmpty())
        {
            return coin;
        }

        var coinInfo = _options.Value.ReflectionItems.FirstOrDefault(t => t.Value == coin);
        return coinInfo.Key ?? coin;
    }
}