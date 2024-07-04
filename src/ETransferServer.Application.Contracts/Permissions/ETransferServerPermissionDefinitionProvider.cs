using ETransferServer.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ETransferServer.Permissions;

public class ETransferServerPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        context.AddGroup(ETransferServerPermissions.GroupName);
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ETransferServerResource>(name);
    }
}
