using System.Collections.Generic;

namespace ETransferServer.Options;

public class ChainOptions
{

    // Indexer block height is available within how many lags
    public int IndexerAvailableHeightBehind { get; set; } = 1300;
    
    // After these times, the trading results are queried directly from the node
    public int TxResultFromNodeSecondsAfter { get; set; } = 900;
    
    public int TxResultFastFromNodeSecondsAfter { get; set; } = 180;
    
    public ContractOption Contract { get; set; }
    public Dictionary<string, ChainInfo> ChainInfos { get; set; } = new();

    

    public class ContractOption
    {
        public int WaitSecondsAfterSend { get; set; } = 5;
        public int DelaySeconds { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 1;
        public int RetryTimes { get; set; } = 0;
        public int SafeBlockHeight { get; set; } = 300;
        public int TransactionTimerMaxSeconds { get; set; } = 3600;
    }
    
    
    public class ChainInfo
    {
        public string BaseUrl { get; set; }
        public bool IsMainChain { get; set; }
        public decimal TransactionFee { get; set; } = 0.0041M;
        public int EstimatedArrivalTime { get; set; } = 160;
        public int EstimatedArrivalFastUpperTime { get; set; } = 60;
        public int EstimatedArrivalFastLowerTime { get; set; } = 10;
        public string ReleaseAccount { get; set; }
        public Dictionary<string, string> ContractAddress { get; set; } = new();
    }
}
