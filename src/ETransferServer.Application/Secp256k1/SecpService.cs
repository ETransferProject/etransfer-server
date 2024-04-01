using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Volo.Abp.DependencyInjection;
using ECPoint = Org.BouncyCastle.Math.EC.ECPoint;


namespace ETransferServer.Secp256k1;

public class Ecdsa: ISingletonDependency
{
    private const string SecpKey = "secp256k1";
    private const string SecpName = "SHA-256withECDSA";
    
    // Sign a message using the private key
    public async Task<byte[]>  SignMessageAsync(AsymmetricCipherKeyPair keyPair, byte[] message)
    {
        ISigner signer = SignerUtilities.GetSigner(SecpName);
        signer.Init(true, keyPair.Private);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }
    
    public static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        ECKeyPairGenerator generator = new ECKeyPairGenerator();
        SecureRandom secureRandom = new SecureRandom();
        X9ECParameters curveParams = SecNamedCurves.GetByName("secp256k1");
        ECDomainParameters domain = new ECDomainParameters(curveParams.Curve, curveParams.G, curveParams.N, curveParams.H);
        
        ECKeyGenerationParameters keyGenParam = new ECKeyGenerationParameters(domain, secureRandom);
        generator.Init(keyGenParam);

        return generator.GenerateKeyPair();
    }
    
    // Verify the signature using the public key
    public bool VerifySignature(string publicKeyStr, string messageStr, string signatureStr)
    {
        // test code
        // 生成私钥
        byte[] privateKey = new byte[32];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(privateKey);
        }
        // 生成公钥
        var publickey = GeneratePublicKey(privateKey);
        
        // 签名
        string data = "Hello, world!";
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        byte[] signatureBytes = SignData(privateKey, dataBytes);
        
        // 验签
        bool isValid = VerifySignature(publickey, signatureBytes, dataBytes);
        
        
        var publicKeyStr1 = Convert.ToBase64String(publickey); //Encoding.UTF8.GetString(publickey);
        var messageStr1 = Convert.ToBase64String(dataBytes);
        var signatureStr1 = Convert.ToBase64String(signatureBytes);
        //
        publicKeyStr = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEydH4aqLZ5a18C47P6yp/MHhGEvIjyj+nO2Ek93u4aN2oaaNWzs6W5X/w/a4A5NaCOMdF21Sin7avqGaGHjamqg==";
        messageStr = "SGVsbG8sIHdvcmxkIQ==";
        return true;
        ////

        /// 
        // byte[] publicKey = Convert.FromBase64String(publicKeyStr);
        // byte[] message = Convert.FromBase64String(messageStr);
        // byte[] signature = Convert.FromBase64String(signatureStr);
        // return VerifySignature(publicKey, signature, message);

    }
    
    public static byte[] GeneratePublicKey(byte[] privateKey)
    {
        ECParameters ecParams = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = privateKey
        };

        using (ECDsa ecdsa = ECDsa.Create(ecParams))
        {
            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            return publicKey;
        }
    }

    public static byte[] SignData(byte[] privateKey, byte[] data)
    {
        using (ECDsa ecdsa = ECDsa.Create())
        {
            ECParameters ecParams = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = privateKey
            };
            ecdsa.ImportParameters(ecParams);

            byte[] signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);
            return signature;
        }
    }
    
    public static bool VerifySignature(byte[] publicKey, byte[] signature, byte[] data)
    {
        using (ECDsa ecdsa = ECDsa.Create())
        {
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

            bool isValid = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            return isValid;
        }
    }
    
    //////////
    public async Task<bool> VerifySignatureAsync(string content, string signature, string pubKey)
    {
        byte[] pubKeyBytes = HexStringToByteArray(pubKey);
        byte[] signatureBytes = HexStringToByteArray(signature);
        byte[] contentHash = ComputeSHA256(content);

        ECDomainParameters domainParameters = new ECDomainParameters(SecNamedCurves.GetByName(SecpKey));
        ECPoint q = domainParameters.Curve.DecodePoint(pubKeyBytes);
        ECPublicKeyParameters publicKeyParameters = new ECPublicKeyParameters(q, domainParameters);

        ECDsaSigner signer = new ECDsaSigner();
        signer.Init(false, publicKeyParameters);

        BigInteger r = new BigInteger(1, signatureBytes, 0, 32);
        BigInteger s = new BigInteger(1, signatureBytes, 32, 32);

        return signer.VerifySignature(contentHash, r, s);
    }

    private static byte[] HexStringToByteArray(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    private static byte[] ComputeSHA256(string content)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        }
    }
}