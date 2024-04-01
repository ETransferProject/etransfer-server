using System.Collections.Generic;
using JetBrains.Annotations;

namespace ETransferServer.Network.Dtos;

public class TransactionNotificationResponse
{
    public bool Success { get; set; }
    public string Result { get; set; }
    public string Exception { get; set; }
}
public class CoboListDto
{
    public List<CoboDto> CoboList { get; set; }
    public string ChainId { get; set; }
}

public class CoboDto
{
    public string Id { get; set; }
    public string Coin { get; set; }
    public string DisplayCode { get; set; }
    public string Description { get; set; }
    public string Decimal { get; set; }
    public string Address { get; set; }
    public string SourceAddress { get; set; }
    public string Side { get; set; }
    public string Amount { get; set; }
    public string AbsAmount { get; set; }
    public string Txid { get; set; }
    public string VoutN { get; set; }
    public string RequestId { get; set; }
    public string Status { get; set; }
    public string AbsCoboFee { get; set; }
    public string CreatedTime { get; set; }
    public string LastTime { get; set; }
    public string RequestCreatedTime { get; set; }
    public int ConfirmedNum { get; set; }
    public TxDetail TxDetail { get; set; }
    public string SourceAddressDetail { get; set; }
    public string ConfirmingThreshold { get; set; }
    public string Type { get; set; }
}

public class TxDetail
{
    public string Txid { get; set; }
    public string Blocknum { get; set; }
    public string Blockhash { get; set; }
    public string Hexstr { get; set; }
}

public class MultiCurrencyDto
{
    public string CoboId { get; set; }
    public string RequestId { get; set; }
    public string Status { get; set; }
    public CoinDetail CoinDetail { get; set; }
    public NftDetail NftDetail { get; set; }
    public AmountDetail AmountDetail { get; set; }
    public FeeDetail FeeDetail { get; set; }
    
    public string SourceAddresses { get; set; }
    public string FromAddress { get; set; }
    public string ToAddress { get; set; }
    
    public string Memo { get; set; }
    public string TxHash { get; set; }
    public string VoutN { get; set; }
    
    public string Nonce { get; set; }
    public string ConfirmedNumber { get; set; }
    public string ReplaceCoboId { get; set; }
    
    public string TransactionType { get; set; }
    public BlockDetail BlockDetail { get; set; }
    public TxDetailHash TxDetailHash { get; set; }
    public string ExtraParameters { get; set; }
    public string CreatedTime { get; set; }
    public string UpdatedTime { get; set; }
    public string FailedReason { get; set; }
    public string ToAddressDetails { get; set; }
}

public class CoinDetail
{
    public string Coin { get; set; }
    public string ChainCode { get; set; }
    public string DisplayCode { get; set; }
    public string Description { get; set; }
    public string Decimal { get; set; }
    public string CanDeposit { get; set; }
    public string CanWithdraw { get; set; }
    public string ConfirmingThreshold { get; set; }
}

public class NftDetail
{
    public string NftCode { get; set; }
    public string TokenId { get; set; }
    public string ChainCode { get; set; }
    public string ContractAddress { get; set; }
}

public class AmountDetail
{
    public string NftCode { get; set; }
    public string AbsAmount { get; set; }
    public string NftAmount { get; set; }
}

public class FeeDetail
{
    public CoinDetail FeeCoinDetail { get; set; }
    public int GasPrice { get; set; }
    public int GasLimit { get; set; }
    public int FeeUsed { get; set; }
    public decimal Fee { get; set; }
}

public class BlockDetail
{
    public int BlockHash { get; set; }
    public int BlockHeight { get; set; }
    public int BlockTime { get; set; }
}

public class TxDetailHash
{
    public int TxHash { get; set; }
}
