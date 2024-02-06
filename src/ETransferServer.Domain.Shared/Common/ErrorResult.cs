using System.Collections.Generic;

namespace ETransferServer.Common;

public static class ErrorResult
{
    public const int FeeExceedCode = 40001;
    public const int FeeExpiredCode = 40002;
    public const int ChainIdInvalidCode = 40003;
    public const int SymbolInvalidCode = 40004;
    public const int AddressInvalidCode = 40005;
    public const int NetworkInvalidCode = 40006;
    public const int JwtInvalidCode = 40007;
    public const int FeeInvalidCode = 40008;
    public const int AmountInsufficientCode = 40009;
    public const int SymbolNullCode = 40010;
    public const int SymbolNotEqualCode = 40011;
    public const int AmountNotEqualCode = 40012;
    public const int WithdrawLimitInsufficientCode = 40013;
    public const int TransactionFailCode = 40014;
    public const int OrderSaveFailCode = 40015;
    public const int AddressFormatWrongCode = 40100;
    public const int NetworkNotSupportCode = 40101;

    public static string GetMessage(int code)
    {
        return ResponseMappings.GetOrDefault(code);
    } 
    
    public static readonly Dictionary<int, string> ResponseMappings = new()
    {
        { 40001, "{network_name} is experiencing a sudden rise in transaction fees. Please initiate the transaction again." },
        { 40002, "Your transaction has expired. Please initiate a new transaction to proceed." },
        { 40003, "Invalid source ChainID. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40004, "Unsupported token. Currently only USDT is supported. Please check and ensure you provide the right token symbol." },
        { 40005, "Unsupported address. Please check and ensure you provide the right withdrawal address." },
        { 40006, "Unsupported network. Please check and ensure you provide the right withdrawal network." },
        { 40007, "Failed to initiate the transaction. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40008, "Invalid transaction fee. Please check and ensure the transaction fee is correct and try again half an hour later." },
        { 40009, "Invalid withdrawal amount. Please check and ensure you enter the right amount (should be greater than the transaction fee)." },
        { 40010, "Unsupported token. Currently only USDT is supported. Please check and ensure you provide the right token symbol." },
        { 40011, "Unsupported token. Currently only USDT is supported. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40012, "Invalid withdrawal amount. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40013, "The remaining withdrawal quota for today is insufficient, with {amount} available. Please try again after {number} hours or consider transferring a smaller amount." },
        { 40014, "Transaction failed. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40015, "Failed to synchronise data. The ETransfer team is actively looking into this issue. Please be assured that your accounts and assets will remain unaffected." },
        { 40100, "Please enter a correct address." },
        { 40101, "If you're withdrawing to {Networks}, please wait a while for the service to be restored and try again later." }
    };
}