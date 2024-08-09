using System;
using System.Collections.Generic;

namespace ETransferServer.ChainsClient.Ton.Helper;

public class ApiHelper
{
    public static class Api
    {
        public static readonly string TonApi = "/v2/blockchain/transactions/{}";
        public static readonly string TonCenter = "/api/v3/transactionsByMessage";
    }
    
    public static Tuple<TonType, string, Dictionary<string, string>> GetApiInfo(string baseUrl, string hash)
    {
        if (baseUrl.ToLower().Contains(TonType.TonApi.ToString().ToLower()))
        {
            return Tuple.Create(TonType.TonApi, baseUrl + Api.TonApi.Replace("{}", hash),  
                new Dictionary<string, string>());
        }
        if (baseUrl.ToLower().Contains(TonType.TonCenter.ToString().ToLower()))
        {
            return Tuple.Create(TonType.TonCenter, baseUrl + Api.TonCenter, new Dictionary<string, string>
            {
                ["msg_hash"] = hash,
                ["direction"] = "in"
            });
        }
        return Tuple.Create(TonType.TonApi, baseUrl + Api.TonApi.Replace("{}", hash),  
            new Dictionary<string, string>());
    }
}

public enum TonType
{
    TonApi,
    TonCenter
}