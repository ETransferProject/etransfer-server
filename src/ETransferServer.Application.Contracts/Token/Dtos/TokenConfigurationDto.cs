using System;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Token.Dtos;

public class TokenConfigurationDto
{
    public string ChainId { get; set; }
    public string Symbol { get; set; }
    public bool DepositEnabled { get; set; } = true;
    public bool WithdrawEnabled { get; set; } = true;
    public bool TransferEnabled { get; set; } = true;
    public DateTime LastUpdatedTime { get; set; }
}

public class SetTokenConfigurationDto
{
    [Required]
    public string ChainId { get; set; }
    
    [Required]
    public string Symbol { get; set; }
    
    public bool? DepositEnabled { get; set; }
    public bool? WithdrawEnabled { get; set; }
    public bool? TransferEnabled { get; set; }
}

public class GetTokenConfigurationRequestDto
{
    [Required]
    public string ChainId { get; set; }
    
    [Required]
    public string Symbol { get; set; }
}
