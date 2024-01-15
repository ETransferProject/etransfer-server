using ETransferServer.Entities;

namespace ETransferServer.Users;

public class UserAddressBase<TKey> : AbstractEntity<TKey>
{
    protected UserAddressBase()
    {
    }

    protected UserAddressBase(TKey id)
        : base(id)
    {
    }
}