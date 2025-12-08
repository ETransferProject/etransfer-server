using ETransferServer.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace ETransferServer.Controllers;

public abstract class ETransferController: AbpControllerBase
{
    protected ETransferController()
    {
        LocalizationResource = typeof(ETransferServerResource);
    }
}