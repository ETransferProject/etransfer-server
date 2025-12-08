using System;
using System.Security.Cryptography;
using System.Text;
using ETransferServer.ThirdPart.CoBo;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Commons;

public class SignTest : ETransferServerApplicationTestBase
{

    public SignTest(ITestOutputHelper output) : base(output)
    {
    }
    
    [Fact]
    public void Test()
    {
        // var sign1 = CoBoHelper.GetSignature("278fa5a8f030b2214889edc8e2b8d2d23baf71e057c4b8a8c6b73322df8cf003", "bbb");
        var sign2 = SignBitcoinTransaction("278fa5a8f030b2214889edc8e2b8d2d23baf71e057c4b8a8c6b73322df8cf003", "bbb");
        // Output.WriteLine(sign1);
        Output.WriteLine(sign2);
        sign2.ShouldBe("304402202b37f55fdff89644c735cd2e9561febbd2fc066b107ec2b85bfcb6d56ca028cd02207b09092332bc111071823ffc0c5448e3e265506e68a589b0683274e3c36837ab");
    }
    
    // depencency:
    //      <PackageReference Include="NBitcoin" Version="7.0.31" />
    public string SignBitcoinTransaction(string privateKeyHex, string transactionBytes)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(privateKeyHex));
        var hash = Hashes.DoubleSHA256(transactionBytes.GetBytes(Encoding.UTF8));
        var signature = privateKey.Sign(hash);
        return Encoders.Hex.EncodeData(signature.ToDER());
    }

    [Fact]
    public void FeishuSign()
    {
        Output.WriteLine(GenSign("W0IC2kwfweTbAvvw10Sumc", 1702979234));
    }
    
    
    private static string GenSign(string secret, int timestamp)
    {
        var stringToSign = timestamp + "\n" + secret;
        var encoding = new UTF8Encoding();
        using var hmacSha256 = new HMACSHA256(encoding.GetBytes(stringToSign));
        var hashMessage = hmacSha256.ComputeHash(Array.Empty<byte>());
        return Convert.ToBase64String(hashMessage);
    }

}