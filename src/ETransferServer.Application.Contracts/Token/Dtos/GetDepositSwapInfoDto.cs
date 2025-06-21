using System.Collections.Generic;

namespace ETransferServer.Token.Dtos;

/// <summary>
/// DTO for deposit swap information
/// </summary>
public class GetDepositSwapInfoDto
{
    /// <summary>
    /// Source token symbol
    /// </summary>
    public string FromSymbol { get; set; }
    
    /// <summary>
    /// List of target tokens available for swap
    /// </summary>
    public List<ToTokenDto> ToTokenList { get; set; } = new();
}

/// <summary>
/// DTO for target token information in swap
/// </summary>
public class ToTokenDto
{
    /// <summary>
    /// Target token symbol
    /// </summary>
    public string Symbol { get; set; }
    
    /// <summary>
    /// List of supported chain IDs for this token
    /// </summary>
    public List<string> ChainIdList { get; set; } = new();
} 