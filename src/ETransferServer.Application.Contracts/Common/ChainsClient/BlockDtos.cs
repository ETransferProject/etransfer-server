using System.Collections.Generic;
using System.Numerics;

namespace ETransferServer.Common.ChainsClient;

public class BlockDtos
{
    public string BlockHash { get; set; }
    public BigInteger BlockHeight { get; set; }
    public BigInteger BlockTimeStamp { get; set; }
    public List<string> TransactionIdList { get; set; }
}