namespace ETransferServer.Common;

public static class CoBoConstant
{
    public const string ClientName = "CoBo";
    public const string ApiKeyName = "BIZ-API-KEY";
    public const string NonceName = "BIZ-API-NONCE";
    public const string SignatureName = "BIZ-API-SIGNATURE";
    public const string ApiSecretName = "ApiSecret";

    public const string GetCoinDetail = "/v1/custody/coin_info/";
    public const string GetAccountDetail = "/v1/custody/org_info/";
    public const string GetAddresses = "/v1/custody/new_addresses/";
    public const string GetTransactionsByTimeEx = "/v1/custody/transactions_by_time_ex/";
    public const string GetTransaction = "/v1/custody/transaction";
    public const string Withdraw = "/v1/custody/new_withdraw_request/";
    public const string WithdrawInfoByRequestId = "/v1/custody/withdraw_info_by_request_id/";

    public const string ResponseNull = "ResponseNull";
    public const int WithdrawQueryInterval = 30;
    
    public static class CoBoTransactionSideEnum
    {
        public const int TransactionDeposit = 1;
        public const int TransactionWithdraw = 2;
    }

    public static class CoBoTransactionStatusEnum
    { 
        public const int PendingApproval = 101; 
        public const int Sent = 201; 
        public const int PendingConfirmation = 501; 
        public const int Success = 900; 
        public const int Failed = 901;
    }


    public static class Order
    {
        public const string Desc = "DESC";
        public const string Asc = "ASC";
    }


    public static class Coins
    {
        public const string USDT = "USDT";
    }
}