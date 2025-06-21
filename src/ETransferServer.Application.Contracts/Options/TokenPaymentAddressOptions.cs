using System.Collections.Generic;

namespace ETransferServer.Options;

public class TokenPaymentAddressOptions
{
    // chain -> token -> address
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
}