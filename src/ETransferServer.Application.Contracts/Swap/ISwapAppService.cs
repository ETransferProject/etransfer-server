using System.Threading.Tasks;
using ETransferServer.Swap.Dtos;

namespace ETransferServer.Swap;

public interface ISwapAppService
{
    decimal GetSlippage(string symbolIn, string symbolOut);
    Task<decimal> GetConversionRate(string chainId, string symbolIn, string symbolOut);
    Task<GetAmountsOutDto> CalculateAmountsOut(string chainId, string symbolIn, string symbolOut, decimal amountIn);
}