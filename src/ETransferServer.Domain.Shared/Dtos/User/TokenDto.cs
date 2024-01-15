using System;

namespace ETransferServer.Dtos.User;

public class TokenDto
{
    public Guid Id { get; set; }
    public string ChainId { get; set; }
    public string Address { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
}