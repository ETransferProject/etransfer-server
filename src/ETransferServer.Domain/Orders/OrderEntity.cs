using ETransferServer.Entities;

namespace ETransferServer.Orders;

public class OrderEntity<TKey> : AbstractEntity<TKey>
{
    protected OrderEntity()
    {
    }

    protected OrderEntity(TKey id)
        : base(id)
    {
    }
}