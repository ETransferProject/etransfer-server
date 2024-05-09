using System;
using ETransferServer.User;

namespace ETransferServer.Common;

public static class InputHelper
{
    public static bool IsDepositSwap(GetUserDepositAddressInput input)
    {
        return !NoDepositSwap(input);
    }
    
    public static bool NoDepositSwap(GetUserDepositAddressInput input)
    {
        return input.ToSymbol.IsNullOrEmpty() || input.Symbol.Equals(input.ToSymbol);
    }
}