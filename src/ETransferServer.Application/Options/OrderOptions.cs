namespace ETransferServer.Options;

public class OrderOptions
{
    public const string Asc = "asc";
    public const string Ascend = "ascend";
    public const string ArrivalTime = "arrivaltime";
    public const string CreateTime = "createtime";
    public const string Rejected = "rejected";
    public const double ValidOrderThreshold = -2d;
    public const double ValidOrderMessageThreshold = -1d;
    public const int MaxResultCount = 100;
    public const int DefaultResultCount = 10;
    public const long DefaultMaxSize = 10000L;
    public const int SubMilliSeconds = 172800000;
}