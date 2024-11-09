using System.Text;
using AElf;
using AElf.ExceptionHandler;
using ETransferServer.Auth.Options;
using ETransferServer.Common;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Solnet.Wallet;

namespace ETransferServer.Auth;

public partial class SignatureGrantHandler
{
    [ExceptionHandler(typeof(Exception), TargetType = typeof(SignatureGrantHandler),
        MethodName = nameof(SignatureGrantHandlerExceptionAsync))]
    public async Task<bool> CheckSignature(string source, string sourceType, byte[] signature, string message,
        byte[] publicKey)
    {
        switch (source)
        {
            case AuthConstant.PortKeySource:
            case AuthConstant.NightElfSource:
                var hash = Encoding.UTF8.GetBytes(message).ComputeHash();
                _logger.LogInformation("hash:{hash}", hash);
                return AElf.Cryptography.CryptoHelper.VerifySignature(signature, hash, publicKey);
            case AuthConstant.WalletSource:
                if (!Enum.TryParse<WalletEnum>(sourceType, true, out var walletType)) return false;
                var addressRaw = Encoding.UTF8.GetString(publicKey);
                switch (walletType)
                {
                    case WalletEnum.EVM:
                        var signatureRaw = Encoding.UTF8.GetString(signature);
                        var messageRaw = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(message));
                        var account = new EthereumMessageSigner().EncodeUTF8AndEcRecover(messageRaw, signatureRaw);
                        return addressRaw == account?.EnsureHexPrefix();
                    case WalletEnum.Solana:
                        var messageByte = Encoding.UTF8.GetBytes(message);
                        var pubKey = new PublicKey(addressRaw);
                        return pubKey.Verify(messageByte, signature);
                    case WalletEnum.TRX:
                        return true;
                    case WalletEnum.TON:
                        return true;
                    default:
                        return false;
                }
            default:
                return false;
        }
    }
    
    public async Task<FlowBehavior> SignatureGrantHandlerExceptionAsync(Exception ex, string source, string sourceType, 
        byte[] signature, string message, byte[] publicKey)
    {
        _logger.LogError(ex, "Signature Check failed.");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}