namespace ETransferServer.Swap.Dtos;

public class GetAmountsOutDto
{
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
    public decimal AmountIn { get; set; }
    public decimal AmountOut { get; set; }
    public decimal MinAmountOut { get; set; }
}