using System;
using System.Globalization;

namespace ETransferServer.Common;

public static class DecimalHelper
{
    public enum RoundingOption
    {
        Round,
        Ceiling,
        Floor,
        Truncate
    }

    public static string ToString(this decimal value, int decimalPlaces, RoundingOption roundingOption = RoundingOption.Round)
    {
        var multiplier = 1M;
        for (var i = 0; i < decimalPlaces; i++)
        {
            multiplier *= 10;
        }
        var roundedValue = roundingOption switch
        {
            RoundingOption.Round => Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero),
            RoundingOption.Ceiling => Math.Ceiling(value * multiplier) / multiplier,
            RoundingOption.Floor => Math.Floor(value * multiplier) / multiplier,
            RoundingOption.Truncate => Math.Truncate(value * multiplier) / multiplier,
            _ => throw new ArgumentOutOfRangeException(nameof(roundingOption), "Invalid rounding option.")
        };

        var isInteger = roundedValue == Math.Floor(roundedValue);
        var formatString = isInteger
            ? "0"
            : $"0.{new string('#', decimalPlaces)}";
        return roundedValue.ToString(formatString, CultureInfo.InvariantCulture);
    }

    public static string ToString(this decimal value, int validDigitalCount, int decimalPlaces,
        RoundingOption roundingOption = RoundingOption.Round)
    {
        var roundedValue = value.ToString(decimalPlaces, roundingOption);
        var integers = (int)(double.Parse(roundedValue) * Math.Pow(10, decimalPlaces));
        var integersCount = integers.ToString().Length - decimalPlaces;
        var decimals = (decimal)Math.Round(Math.Truncate(double.Parse(roundedValue) * Math.Pow(10, validDigitalCount - integersCount)) 
                                          / Math.Pow(10, validDigitalCount - integersCount), decimalPlaces);
        return decimals.ToString();
    }


}