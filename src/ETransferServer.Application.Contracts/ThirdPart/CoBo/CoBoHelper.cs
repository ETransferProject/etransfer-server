using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using ETransferServer.Common;

namespace ETransferServer.ThirdPart.CoBo;

public static class CoBoHelper
{
    public static string GetSignatureContent(HttpMethod httpMethod, string uri,
        Dictionary<string, string> paramDic, long nonce)
    {
        var uris = uri.Split('?');
        var uriParam = uri.Contains('?') ? uris.Last() : string.Empty;
        return GetPlainText(httpMethod, uris.First(), paramDic, uriParam, nonce);
    }

    private static string GetPlainText(HttpMethod httpMethod, string uri, Dictionary<string, string> paramDic,
        string uriParam, long nonce)
    {
        var plainText = GetPlainTest(httpMethod, paramDic, uriParam);
        var requestType = GetRequestType(httpMethod);

        return $"{requestType}|{uri}|{nonce}|{plainText}";
    }

    private static string GetRequestType(HttpMethod httpMethod)
    {
        var requestType = string.Empty;
        if (httpMethod == HttpMethod.Get)
        {
            requestType = RequestType.GET.ToString();
        }
        else if (httpMethod == HttpMethod.Post)
        {
            requestType = RequestType.POST.ToString();
        }

        return requestType;
    }

    private static string GetPlainTest(HttpMethod httpMethod, Dictionary<string, string> paramDic,
        string uriParam)
    {
        var plainText = string.Empty;
        if (httpMethod == HttpMethod.Get)
        {
            if (uriParam.IsNullOrWhiteSpace()) return plainText;
            if (!uriParam.Contains("&")) return uriParam;

            var paramList = uriParam.Split('&').Where(t => !t.IsNullOrWhiteSpace()).OrderBy(f => f).ToList();
            return GetParamText(paramList);
        }
        else if (httpMethod == HttpMethod.Post)
        {
            if (paramDic.IsNullOrEmpty()) return plainText;
            return GetParamText(paramDic);
        }

        return plainText;
    }

    private static string GetParamText(List<string> paramList)
    {
        var paramText = string.Empty;
        foreach (var param in paramList)
        {
            paramText = paramText + "&" + param;
        }

        return paramText.Trim('&');
    }

    private static string GetParamText(Dictionary<string, string> paramDic)
    {
        var paramText = string.Empty;

        var keyValuePairs = new SortedDictionary<string, string>(paramDic);
        foreach (var keyValuePair in keyValuePairs)
        {
            paramText = paramText + $"{keyValuePair.Key}={keyValuePair.Value}&";
        }

        return paramText.TrimEnd('&');
    }

    public static CoinNetwork MatchNetwork(string coBoCoin, Dictionary<string, string> mapping)
    {
        var vals = coBoCoin.Split(CommonConstant.Underline);
        AssertHelper.NotEmpty(vals, "CoBo coin {coBoCoin} invalid", coBoCoin);
        var coinNetwork = mapping.GetValueOrDefault(coBoCoin);
        AssertHelper.NotEmpty(coinNetwork, "CoBo coin {Coin} not support", coBoCoin);
        var network = CoinNetwork.Of(coinNetwork);
        network.CoBoNetwork = vals[0];
        return network;
    }

    public static string MatchCoin(string symbol, string network, Dictionary<string, string> mapping)
    {
        var val = new CoinNetwork(symbol, network).ToText();
        var coin = mapping.Where(kv => kv.Value == val).Select(kv => kv.Key).FirstOrDefault();
        AssertHelper.NotEmpty(coin, "CoBo coin not support for {Val}", val);
        return coin;
    }


    public class CoinNetwork
    {
        public string Symbol { get; set; }
        public string Network { get; set; }
        public string CoBoNetwork { get; set; }

        public CoinNetwork(string symbol, string network)
        {
            Symbol = symbol;
            Network = network;
        }

        public string ToText()
        {
            return string.Join(CommonConstant.At, Symbol, Network);
        }

        public static CoinNetwork Of(string val)
        {
            var vals = val.Split(CommonConstant.At);
            return new CoinNetwork(
                vals.Length > 0 ? vals[0] : CommonConstant.EmptyString,
                vals.Length > 1 ? vals[1] : CommonConstant.EmptyString
            );
        }
    }
}