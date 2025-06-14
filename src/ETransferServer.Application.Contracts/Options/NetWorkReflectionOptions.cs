using System.Collections.Generic;

namespace ETransferServer.Options;

/// <summary>
/// Configuration options for network reflection and mapping services
/// Handles network identifier mappings and symbol translations
/// </summary>
public class NetWorkReflectionOptions
{
    /// <summary>
    /// Network reflection mappings for identifier translation
    /// Key: Internal network identifier, Value: External network identifier
    /// Used to map between different network naming conventions across services
    /// Example: "ETH" -> "Ethereum", "BSC" -> "BinanceSmartChain"
    /// </summary>
    public Dictionary<string, string> ReflectionItems { get; set; } = new();

    /// <summary>
    /// Symbol reflection mappings for token identifier translation
    /// Key: Internal token symbol, Value: External token symbol
    /// Used to map between different token naming conventions across services
    /// Example: "USDT" -> "TetherUSD", "BTC" -> "Bitcoin"
    /// </summary>
    public Dictionary<string, string> SymbolItems { get; set; } = new();
    
    /// <summary>
    /// Contract address mappings for cross-network token identification
    /// Key: Network-Symbol combination, Value: Contract address
    /// Format: "NETWORK_SYMBOL" -> "0x..."
    /// </summary>
    public Dictionary<string, string> ContractMappings { get; set; } = new();
    
    /// <summary>
    /// Deprecated network identifiers that should be mapped to new ones
    /// Key: Deprecated identifier, Value: New identifier
    /// Used for backward compatibility
    /// </summary>
    public Dictionary<string, string> DeprecatedMappings { get; set; } = new();
    
    /// <summary>
    /// Helper method to get the reflected network identifier
    /// </summary>
    /// <param name="networkId">Original network identifier</param>
    /// <returns>Reflected network identifier or original if no mapping exists</returns>
    public string GetReflectedNetwork(string networkId)
    {
        return ReflectionItems.TryGetValue(networkId, out var reflected) ? reflected : networkId;
    }
    
    /// <summary>
    /// Helper method to get the reflected symbol
    /// </summary>
    /// <param name="symbol">Original token symbol</param>
    /// <returns>Reflected symbol or original if no mapping exists</returns>
    public string GetReflectedSymbol(string symbol)
    {
        return SymbolItems.TryGetValue(symbol, out var reflected) ? reflected : symbol;
    }
    
    /// <summary>
    /// Helper method to get contract address for a network-symbol combination
    /// </summary>
    /// <param name="network">Network identifier</param>
    /// <param name="symbol">Token symbol</param>
    /// <returns>Contract address or null if not found</returns>
    public string GetContractAddress(string network, string symbol)
    {
        var key = $"{network}_{symbol}";
        return ContractMappings.TryGetValue(key, out var address) ? address : null;
    }
}