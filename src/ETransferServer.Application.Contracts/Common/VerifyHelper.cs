using System;
using System.Linq;
using System.Text.RegularExpressions;
using AElf;
using AElf.Cryptography;
using AElf.Types;
using Google.Protobuf;

namespace ETransferServer.Common;

public static class VerifyHelper
{
    public static bool VerifyEmail(string address)
    {
        // string emailRegex =
        //     @"([a-zA-Z0-9_\.\-])+\@(([a-zA-Z0-9\-])+\.)+([a-zA-Z0-9]{2,5})+";

        var emailRegex = @"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$";
        var emailReg = new Regex(emailRegex);
        return emailReg.IsMatch(address.Trim());
    }

    public static bool VerifyPhone(string phoneNumber)
    {
        var phoneRegex = @"^1[0-9]{10}$";
        var emailReg = new Regex(phoneRegex);
        return emailReg.IsMatch(phoneNumber.Trim());
    }

    public static bool VerifySignature(Transaction transaction, string inputPublicKey)
    {
        if (!transaction.VerifyFields()) return false;

        var recovered = CryptoHelper.RecoverPublicKey(transaction.Signature.ToByteArray(),
            transaction.GetHash().ToByteArray(), out var publicKey);

        return recovered && Address.FromPublicKey(publicKey) == transaction.From &&
               ByteString.CopyFrom(publicKey).ToHex() == inputPublicKey;
    }
    
    
    public static bool IsPhone(string input)
    {
        var pattern = @"^\+\d+$";
        return Regex.IsMatch(input, pattern);
    }

    public static bool IsEmail(string input)
    {
        return input.Count(c => c == '@') == 1;
    }
    
    public static bool VerifyMemoVersion(string version, string anotherVersion)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            return new Version(version.ToLower().Replace(CommonConstant.V, string.Empty))
                   >= new Version(anotherVersion);
        }
        catch (Exception e)
        {
            return false;
        }
    }
}