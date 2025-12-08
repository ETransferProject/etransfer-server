using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ETransferServer.ChainsClient.Solana.Helper;

public abstract class JsonRpcBase
{
    /// <summary>
    /// The rpc version.
    /// </summary>
    public string Jsonrpc { get; protected set; }

    /// <summary>
    /// The id of the message.
    /// </summary>
    public int Id { get; set; }
}

public class JsonRpcRequest : JsonRpcBase
{
    /// <summary>
    /// The request method.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// The method parameters list.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<object> Params { get; }

    internal JsonRpcRequest(int id, string method, IList<object> parameters)
    {
        Params = parameters;
        Method = method;
        Id = id;
        Jsonrpc = "2.0";
    }
}