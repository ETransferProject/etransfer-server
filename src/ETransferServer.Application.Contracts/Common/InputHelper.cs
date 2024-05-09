using System;
using ETransferServer.User;
using JetBrains.Annotations;

namespace ETransferServer.Common;

public static class InputHelper
{
    public static bool IsDepositSwap(string fromSymbol, [CanBeNull] string toSymbol)
    {
        return !NoDepositSwap(fromSymbol, toSymbol);
    }

    public static bool NoDepositSwap(string fromSymbol, [CanBeNull] string toSymbol)
    {
        return toSymbol.IsNullOrEmpty() || fromSymbol.Equals(toSymbol);
    }
}