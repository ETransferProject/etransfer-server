namespace ETransferServer.Grains.Provider;

public interface ISwapProvider
{
    decimal ParseReturnValue(string returnValue);
    Task<(decimal, decimal)> GetAmountsOutAsync(string symbolIn, string symbolOut, decimal amountIn);

}

public class SwapProvider :ISwapProvider
{
    public decimal ParseReturnValue(string returnValue)
    {
        throw new NotImplementedException();
    }

    public Task<(decimal, decimal)> GetAmountsOutAsync(string symbolIn, string symbolOut, decimal amountIn)
    {
        throw new NotImplementedException();
    }
}