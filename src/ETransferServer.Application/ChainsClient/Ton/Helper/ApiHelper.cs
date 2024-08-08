using System;
using System.Collections.Generic;

namespace ETransferServer.ChainsClient.Ton.Helper;

public class ApiHelper
{
    public static class Api
    {
        public static readonly string TonScan = "/api/bt/getTransactionGraphByHash";
        public static readonly string TonCenter = "/api/v3/transactions";
        public static readonly string TonApi = "/v2/blockchain/transactions/{}";
    }
    
    public static Tuple<TonType, string, Dictionary<string, string>> GetApiInfo(string baseUrl, string hash)
    {
        if (baseUrl.ToLower().Contains(TonType.TonScan.ToString().ToLower()))
        {
            return Tuple.Create(TonType.TonScan, baseUrl + Api.TonScan, new Dictionary<string, string>
            {
                ["tx_hash"] = hash
            });
        }
        if (baseUrl.ToLower().Contains(TonType.TonCenter.ToString().ToLower()))
        {
            return Tuple.Create(TonType.TonCenter, baseUrl + Api.TonCenter, new Dictionary<string, string>
            {
                ["hash"] = hash
            });
        }
        if (baseUrl.ToLower().Contains(TonType.TonApi.ToString().ToLower()))
        {
            return Tuple.Create(TonType.TonApi, baseUrl + Api.TonApi.Replace("{}", hash),  
                new Dictionary<string, string>());
        }
        return Tuple.Create(TonType.TonScan, baseUrl + Api.TonScan, new Dictionary<string, string>
        {
            ["tx_hash"] = hash
        });
    }
}

public enum TonType
{
    TonScan,
    TonCenter,
    TonApi
}