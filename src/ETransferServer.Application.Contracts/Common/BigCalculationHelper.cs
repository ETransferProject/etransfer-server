using System;
using System.Numerics;

namespace ETransferServer.Common
{
    public static class BigCalculationHelper
    {
       public static string CalculateAmount(decimal amount, int decimals, string defaultValue = "0")
        {
            return BigInteger.TryParse((amount * (decimal)Math.Pow(10, decimals)).ToString("0"), out var result) ? result.ToString() : defaultValue;
        }
    }
}