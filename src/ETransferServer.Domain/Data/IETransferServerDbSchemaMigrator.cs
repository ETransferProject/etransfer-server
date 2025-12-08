using System.Threading.Tasks;

namespace ETransferServer.Data;

public interface IETransferServerDbSchemaMigrator
{
    Task MigrateAsync();
}
