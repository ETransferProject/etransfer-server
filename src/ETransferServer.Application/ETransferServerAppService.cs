using ETransferServer.Localization;
using Volo.Abp.Application.Services;

namespace ETransferServer;

/* Inherit your application services from this class.
 */
public abstract class ETransferServerAppService : ApplicationService
{
    protected ETransferServerAppService()
    {
        LocalizationResource = typeof(ETransferServerResource);
    }
}
