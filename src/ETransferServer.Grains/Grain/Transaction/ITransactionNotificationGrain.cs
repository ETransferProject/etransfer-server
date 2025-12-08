namespace ETransferServer.Grains.Grain.Worker.Transaction;

public interface ITransactionNotificationGrain : IGrainWithGuidKey
{
    Task<bool> TransactionNotification(string timestamp, string signature, string body);
}