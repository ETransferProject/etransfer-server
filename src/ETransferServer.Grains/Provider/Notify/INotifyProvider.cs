using ETransferServer.Dtos.Notify;

namespace ETransferServer.Grains.Provider.Notify;

public interface INotifyProvider
{

    public NotifyTypeEnum NotifyType();
    
    public Task<bool> SendNotifyAsync(NotifyRequest notifyRequest);
    
}

public enum NotifyTypeEnum
{
    Email = 0,
    FeiShuGroup = 1,
}