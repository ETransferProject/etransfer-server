namespace ETransferServer.Grains.Provider;

public interface ISwapProvider
{
    Task<(decimal, decimal)> GetAmountsOutAsync(string chainId, string symbolIn, string symbolOut, decimal amountIn);
}

public class SwapProvider : ISwapProvider
{
    public Task<(decimal, decimal)> GetAmountsOutAsync(string chainId, string symbolIn, string symbolOut,
        decimal amountIn)
    {
        throw new NotImplementedException();
    }
}