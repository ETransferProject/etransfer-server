using KafkaNet.Common;
using OtpNet;

namespace ETransferServer.Common;

public class TotpHelper
{
    public static string GetCode(string secret)
    {
        var base32String = Base32Encoding.ToString(secret.ToBytes());
        var base32Bytes = Base32Encoding.ToBytes(base32String);
        var totp = new Totp(base32Bytes);
        return totp.ComputeTotp();
    }
}