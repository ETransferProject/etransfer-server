
namespace ETransferServer.Common;

public static class DepositSwapAmountHelper
{
    private const decimal AmountZero = 0m;
    private const decimal AmountOneBillion = 1000000000m;

    public static bool IsValidRange(decimal amount)
    {
        return amount > AmountZero && amount < AmountOneBillion;
    }
}