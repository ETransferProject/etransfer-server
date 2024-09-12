using System.Collections.Generic;
using ETransferServer.Common;

namespace ETransferServer.Options;

public class BlockChainInfoOptions
{
    public int TimeOut { get; set; } = 10;
    public Dictionary<string, ChainInfos> ChainInfos { get; set; }
}

public class ChainInfos
{
    public string Api { get; set; }
    public BlockchainType ChainType { get; set; }
}