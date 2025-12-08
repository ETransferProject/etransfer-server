using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Solana
{
    public class SolanaResponse
    {
        [JsonProperty("jsonrpc")] public string json { get; set; }
        /// <summary>
        /// The slot this transaction was processed in.
        /// </summary>
        [JsonProperty("result")] public TransactionResult Result { get; set; }
    }

    /// <summary>
    /// Represents the tuple transaction and metadata.
    /// </summary>
    public class TransactionResult
    {
        /// <summary>
        /// The transaction information.
        /// </summary>
        [JsonProperty("blockTime")] public long BlockTime { get; set; }
        
    }

    public enum Commitment
    {
        /// <summary>
        /// The node will query the most recent block confirmed by supermajority of the cluster as having reached maximum lockout, meaning the cluster has recognized this block as finalized.
        /// </summary>
        Finalized,
        /// <summary>
        /// The node will query the most recent block that has been voted on by supermajority of the cluster.
        /// </summary>
        Confirmed,

        /// <summary>
        /// The node will query its most recent block. Note that the block may not be complete.
        /// </summary>
        Processed
    }
}