namespace ETransferServer.Common.ChainsClient;

public interface IBlockchainClientFactory<T> 
    where T : class
{
    T GetClient(string chainId);
}