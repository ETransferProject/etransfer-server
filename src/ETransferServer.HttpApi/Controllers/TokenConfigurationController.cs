using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Token;
using ETransferServer.Token.Dtos;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("TokenConfiguration")]
[Route("api/etransfer/token-configuration")]
public class TokenConfigurationController : ETransferController
{
    private readonly ITokenConfigurationAppService _tokenConfigurationAppService;

    public TokenConfigurationController(ITokenConfigurationAppService tokenConfigurationAppService)
    {
        _tokenConfigurationAppService = tokenConfigurationAppService;
    }

    /// <summary>
    /// Get token configuration status
    /// </summary>
    /// <param name="request">Request parameters</param>
    /// <returns>Configuration status</returns>
    [HttpGet("")]
    public async Task<TokenConfigurationDto> GetTokenConfigurationAsync([FromQuery] GetTokenConfigurationRequestDto request)
    {
        return await _tokenConfigurationAppService.GetTokenConfigurationAsync(request);
    }

    /// <summary>
    /// Set token deposit enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="request">Enable status request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("deposit/{chainId}/{symbol}")]
    public async Task<TokenConfigurationDto> SetDepositEnabledAsync(
        [FromRoute] string chainId, 
        [FromRoute] string symbol, 
        [FromBody] SetEnabledRequestDto request)
    {
        return await _tokenConfigurationAppService.SetDepositEnabledAsync(chainId, symbol, request.Enabled);
    }

    /// <summary>
    /// Set token withdraw enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="request">Enable status request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("withdraw/{chainId}/{symbol}")]
    public async Task<TokenConfigurationDto> SetWithdrawEnabledAsync(
        [FromRoute] string chainId, 
        [FromRoute] string symbol, 
        [FromBody] SetEnabledRequestDto request)
    {
        return await _tokenConfigurationAppService.SetWithdrawEnabledAsync(chainId, symbol, request.Enabled);
    }

    /// <summary>
    /// Set token transfer enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="request">Enable status request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("transfer/{chainId}/{symbol}")]
    public async Task<TokenConfigurationDto> SetTransferEnabledAsync(
        [FromRoute] string chainId, 
        [FromRoute] string symbol, 
        [FromBody] SetEnabledRequestDto request)
    {
        return await _tokenConfigurationAppService.SetTransferEnabledAsync(chainId, symbol, request.Enabled);
    }

    /// <summary>
    /// Batch set token configuration
    /// </summary>
    /// <param name="request">Configuration request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("")]
    public async Task<TokenConfigurationDto> SetTokenConfigurationAsync([FromBody] SetTokenConfigurationDto request)
    {
        return await _tokenConfigurationAppService.SetTokenConfigurationAsync(request);
    }

    /// <summary>
    /// Batch enable/disable all token functions
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="request">Enable status request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("all/{chainId}/{symbol}")]
    public async Task<TokenConfigurationDto> SetAllEnabledAsync(
        [FromRoute] string chainId, 
        [FromRoute] string symbol, 
        [FromBody] SetEnabledRequestDto request)
    {
        var setRequest = new SetTokenConfigurationDto
        {
            ChainId = chainId,
            Symbol = symbol,
            DepositEnabled = request.Enabled,
            WithdrawEnabled = request.Enabled,
            TransferEnabled = request.Enabled
        };
        
        return await _tokenConfigurationAppService.SetTokenConfigurationAsync(setRequest);
    }
}

/// <summary>
/// Set enabled status request DTO
/// </summary>
public class SetEnabledRequestDto
{
    /// <summary>
    /// Whether to enable
    /// </summary>
    public bool Enabled { get; set; }
}
