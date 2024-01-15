using Volo.Abp.Application.Dtos;

namespace ETransferServer.Models;

public class GetTokenListRequestDto 
{
    public string Type { get; set; }
    public string ChainId { get; set; }
}
