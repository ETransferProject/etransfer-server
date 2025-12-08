using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Data;

/* This is used if database provider does't define
 * IETransferServerDbSchemaMigrator implementation.
 */
public class NullETransferServerDbSchemaMigrator : IETransferServerDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
