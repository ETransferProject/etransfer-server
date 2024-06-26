using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ETransferServer.User;

namespace ETransferServer.Common;

public static class GuidHelper
{
    
    /// <summary>
    ///     Generate an uniqId with params
    /// </summary>
    /// <param name="paramArr"></param>
    /// <returns></returns>
    public static Guid UniqGuid(params string[] paramArr)
    {
        return new Guid(MD5.HashData(Encoding.Default.GetBytes(GenerateId(paramArr))));
    }

    /// <summary>
    ///     Generate a string id
    /// </summary>
    /// <param name="paramArr"></param>
    /// <returns></returns>
    public static string GenerateId(params string[] paramArr)
    {
        return string.Join("_", paramArr);
    }
    
    public static string GenerateGrainId(params object[] ids)
    {
        return ids.JoinAsString("-");
    }
    
    public static string GenerateCombinedId(params string[] parts)
    {
        return string.Join(CommonConstant.Colon, parts);
    }
}