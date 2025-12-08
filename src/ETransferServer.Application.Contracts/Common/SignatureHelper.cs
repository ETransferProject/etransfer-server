using System;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace ETransferServer.Common;

public static class SignatureHelper
{
    public static bool VerifySignature(string content, string signature, string publicKey)
    {
        if (!PubKey.TryCreatePubKey(Encoders.Hex.DecodeData(publicKey), out var pk))
        {
            return false;
        }

        var sig = Encoders.Hex.DecodeData(signature);
        return pk.Verify(Hashes.DoubleSHA256(content.GetBytes(Encoding.UTF8)), ECDSASignature.FromDER(sig));
    }
}