using System.Collections.Generic;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

public class AddressesDto
{
    public string Coin { get; set; }
    public List<string> Addresses { get; set; }
}