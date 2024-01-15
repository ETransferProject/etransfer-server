using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace ETransferServer.Auth;

[Dependency(ReplaceServices = true)]
public class ETransferServerBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "ETransferServer";
}
