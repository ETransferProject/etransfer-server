using System.Threading.Tasks;
using ETransferServer.Token.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Token;

public interface ITokenConfigurationAppService : IApplicationService
{
    /// <summary>
    /// Get token configuration status
    /// </summary>
    /// <param name="request">Request parameters</param>
    /// <returns>Configuration status</returns>
    Task<TokenConfigurationDto> GetTokenConfigurationAsync(GetTokenConfigurationRequestDto request);
    
    /// <summary>
    /// Set token deposit enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="enabled">Whether to enable</param>
    /// <returns>Updated configuration</returns>
    Task<TokenConfigurationDto> SetDepositEnabledAsync(string chainId, string symbol, bool enabled);
    
    /// <summary>
    /// Set token withdraw enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="enabled">Whether to enable</param>
    /// <returns>Updated configuration</returns>
    Task<TokenConfigurationDto> SetWithdrawEnabledAsync(string chainId, string symbol, bool enabled);
    
    /// <summary>
    /// Set token transfer enabled status
    /// </summary>
    /// <param name="chainId">Chain ID</param>
    /// <param name="symbol">Token symbol</param>
    /// <param name="enabled">Whether to enable</param>
    /// <returns>Updated configuration</returns>
    Task<TokenConfigurationDto> SetTransferEnabledAsync(string chainId, string symbol, bool enabled);
    
    /// <summary>
    /// Batch set token configuration
    /// </summary>
    /// <param name="request">Configuration request</param>
    /// <returns>Updated configuration</returns>
    Task<TokenConfigurationDto> SetTokenConfigurationAsync(SetTokenConfigurationDto request);
}
