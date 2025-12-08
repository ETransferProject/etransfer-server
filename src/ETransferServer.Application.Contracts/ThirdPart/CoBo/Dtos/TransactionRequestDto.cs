using Newtonsoft.Json;
using ETransferServer.Common;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

public class TransactionRequestDto
{
    
    // Coin codes. Separated by commas
    public string Coins { get; set; }

    /// <see cref="ETransferServer.Common.CoBoConstant.CoBoTransactionSideEnum"/>
    public int Side { get; set; }

    /// <see cref="ETransferServer.Common.CoBoConstant.CoBoTransactionStatusEnum"/>
    public int Status { get; set; }

    //Begin timestamp(milliseconds).
    //If set, transactions whose transaction created time is GREATER THAN OR EQUAL TO this will be returned.
    [JsonProperty("begin_time")] public long BeginTime { get; set; }

    // End timestamp (milliseconds).
    // If set, the transactions whose created time is LESS THAN this will be returned.
    [JsonProperty("end_time")] public long EndTime { get; set; }
    
    // Sorting method. Default: created_time; other option: last_time
    [JsonProperty("order_by")] public string OrderBy { get; set; }

    // Page size.
    // If not set, the default size will be 50, and the maximum size will also be 50.
    public int Limit { get; set; }

    // Offset specifies the starting index for the current query,
    // indicating the number of transactions to skip before fetching and returning the transactions in the result.
    public int Offset { get; set; }
    
    /// Sorting order. <see cref="ETransferServer.Common.CoBoConstant.Order"/>
    public string Order { get; set; } = CoBoConstant.Order.Asc;
}