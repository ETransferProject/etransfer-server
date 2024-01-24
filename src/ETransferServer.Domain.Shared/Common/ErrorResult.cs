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
    public const int SymbolErrorCode = 40010;
    public const int AmountErrorCode = 40011;
    public const int WithdrawLimitInsufficientCode = 40012;
    public const int TxFailCode = 40013;
    public const int OrderParamInvalidCode = 40014;
    public const int OrderSaveFailCode = 40015;
    public const int AddressFormatWrongCode = 40100;
    public const int NetworkNotSupportCode = 40101;

    public static string GetMessage(int code)
    {
        return ResponseMappings.GetOrDefault(code);
    } 
    
    public static readonly Dictionary<int, string> ResponseMappings = new()
    {
        { 40001, "The transaction fee on the {network_name} rose suddenly due to the gas price fluctruation on the destination chain. Please initiate a new transaction and try again later." },
        { 40002, "Your transaction has expired. Please initiate a new transaction to proceed." },
        { 40003, "Invalid source ChainID received. Please wait as we investigate this matter. You will not encounter any asset loss, your transaction safety is our priority." },
        { 40004, "Unsupported token {received_token_symbol} received, only support USDT is supported currently. You will not encounter any asset loss, your transaction safety is our priority." },
        { 40005, "Unsupported address received, please check the receiving address. You will not encounter any asset loss, your transaction safety is our priority." },
        { 40006, "Unsupported network received, please check the withdrawal network. You will not encounter any asset loss, your transaction safety is our priority." },
        { 40007, "Failed to create the transaction" },
        { 40008, "Wrong transaction fee." },
        { 40009, "Insufficient sending amount. Please ensure that your withdrawal amount is correct (it should be greater than the transaction fee)." },
        { 40010, "Invalid token symbol, only USDT is supported currently. Please check the symbol of the token." },
        { 40011, "Invalid sending amount. Please ensure that your withdrawal amount is correct (it should be greater than the transaction fee)." },
        { 40012, "The withdrawal limit available today is insufficient, with only {amount} remaining. You can withdraw a smaller amount or try again after {number} hours." },
        { 40013, "Transaction is built error." },
        { 40014, "Failed to create the order. Please check the transaction input and try again." },
        { 40015, "Failed to synchronize the order." },
        { 40100, "Please enter a correct address." },
        { 40101, "{Networks} is currently not supported." }
    };
}