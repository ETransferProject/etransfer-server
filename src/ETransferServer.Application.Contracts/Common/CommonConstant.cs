namespace ETransferServer.Common;

public static class CommonConstant
{
    public const string Space = " ";
    public const string EmptyString = "";
    public const string Dot = ".";
    public const string Hyphen = "-";
    public const string Colon = ":";
    public const string Underline = "_";
    public const string Comma = ",";
    public const string At = "@";
    public const string Slash = "/";
    public const string WithdrawRequestErrorKey = "WithdrawRequestErrorKey";
    public const string WithdrawThirdPartErrorKey = "WithdrawThirdPartError";
    public const string SignatureClientName = "Signature";
    public const string ThirdPartSignUrl = "/api/app/signature/thirdPart";
    public const string SuccessStatus = "success";
    public const string Withdraw = "withdraw";
    public const string Deposit = "deposit";
    
    public const string DepositOrderLostAlarm = "DepositOrderLostAlarm";
    public const string DepositOrderCoinNotSupportAlarm = "DepositOrderCoinNotSupportAlarm";
    
    public const string PortKeyAppId = "PortKey";
    public const string NightElfAppId = "NightElf";
    
    public static class ChainId
    {
        public const string AElfMainChain = "AELF";
        public const string AElfSideChainTdvv = "tDVV";
    }

    public static class Network
    {
        public const string AElf = "AELF";
        public const string ETH = "ETH";
    }
    
    public static class NetworkStatus
    {
        public const string Health = "Health";
        public const string Offline = "Offline";
    }

    public static class Symbol
    {
        public const string Elf = "ELF";
        public const string USD = "USD";
        public const string USDT = "USDT";
        public const string SGR = "SGR-1";
    }

    public static class StreamConstant
    {
        public const string MessageStreamNameSpace = "ETransferServer";

    }
    
    public static class TransactionState
    {
        public const string Mined = "MINED";
        public const string Pending = "PENDING";
        public const string NotExisted = "NOTEXISTED";
        public const string Failed = "FAILED";
        public const string NodeValidationFailed = "NODEVALIDATIONFAILED";
    }
    
    public static class ThirdPartResponseCode
    {
        public const string DuplicateRequest = "12009";
    }
}