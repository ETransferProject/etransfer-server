using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Options;
using Google.Protobuf;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace ETransferServer.Common.AElfSdk;

public interface IContractProvider
{
    Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId, string senderAddress,
        string contractName, string methodName,
        IMessage param, string contractAddress = null);

    Task<Tuple<bool, string>> SendTransactionAsync(string chainId, Transaction transaction);

    Task<T> CallTransactionAsync<T>(string chainId, string contractName, string methodName,
        IMessage param, string contractAddress = null) where T : class;

    Task<T> CallTransactionAsync<T>(string chainId, Transaction transaction) where T : class;

    Task<TransactionResultDto> QueryTransactionResultAsync(string chainId, string transactionId);

    Task<ChainStatusDto> GetChainStatusAsync(string chainId);
    
    Task<BlockDto> GetBlockAsync(string chainId, string blockHash);

    Task<TransactionResultDto> WaitTransactionResultAsync(string chainId, string transactionId,
        int maxWaitMillis = 5000, int delayMillis = 1000);

    Task<string> GetContractAddressAsync(string chainId, string contractName);
}

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
    {
        Converters = new List<JsonConverter> { new AddressConverter() }
    };

    private readonly ECKeyPair _internalKeyPair =
        GetAElfKeyPair("0befd38e40290de677312eceb0927e3e3538d93c2f3b889bbf0cb70176d68b98");

    private readonly Dictionary<string, AElfClient> _clients = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _contractAddress = new();
    private readonly SignatureProvider _signatureProvider;

    private readonly IOptionsSnapshot<ChainOptions> _chainOption;
    private readonly ILogger<ContractProvider> _logger;

    public ContractProvider(IOptionsSnapshot<ChainOptions> chainOption, ILogger<ContractProvider> logger,
        SignatureProvider signatureProvider)
    {
        _logger = logger;
        _signatureProvider = signatureProvider;
        _chainOption = chainOption;
        InitAElfClient();
    }

    private static ECKeyPair GetAElfKeyPair(string privateKeyHex) =>
        CryptoHelper.FromPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKeyHex));

    private void InitAElfClient()
    {
        if (_chainOption.Value.ChainInfos.IsNullOrEmpty())
        {
            return;
        }

        foreach (var node in _chainOption.Value.ChainInfos)
        {
            _clients[node.Key] = new AElfClient(node.Value.BaseUrl);
            _logger.LogInformation("init AElfClient: {ChainId}, {Node}", node.Key, node.Value.BaseUrl);
        }
    }

    private AElfClient Client(string chainId)
    {
        AssertHelper.IsTrue(_clients.ContainsKey(chainId), "AElfClient of {chainId} not found.", chainId);
        return _clients[chainId];
    }

    public async Task<string> GetContractAddressAsync(string chainId, string contractName)
    {
        var contractAddress = _contractAddress.GetOrAdd(chainId, _ => new ConcurrentDictionary<string, string>());
        var addressResult = contractAddress.GetOrAdd<string, string>(contractName, name => new Lazy<string>(() =>
        {
            var chainInfoExists = _chainOption.Value.ChainInfos.TryGetValue(chainId, out var chainInfo);
            AssertHelper.IsTrue(chainInfoExists, "ChainId {ChainId} not exists in option");
            var address = chainInfo?.ContractAddress.GetValueOrDefault(contractName, CommonConstant.EmptyString);
            if (address.IsNullOrEmpty() && SystemContractName.All.Contains(name))
            {
                address = AsyncHelper.RunSync(() =>
                    Client(chainId).GetContractAddressByNameAsync(HashHelper.ComputeFrom(contractName))).ToBase58();
            }

            AssertHelper.NotEmpty(address, "Address of contract {contractName} on {chainId} not exits.",
                name, chainId);
            _logger.LogInformation(
                "Contract address saved: chainId={ChainId}, contractName={ContractName}, address={Address}", chainId,
                contractName, address);
            return address;
        }).Value);

        _logger.LogDebug(
            "Contract address: chainId={ChainId}, contractName={ContractName}, address={Address}", chainId,
            contractName, addressResult);
        return addressResult;
    }

    public async Task<TransactionResultDto> QueryTransactionResultAsync(string chainId, string transactionId)
    {
        return await Client(chainId).GetTransactionResultAsync(transactionId);
    }

    public async Task<ChainStatusDto> GetChainStatusAsync(string chainId)
    {
        return await Client(chainId).GetChainStatusAsync();
    }

    public async Task<BlockDto> GetBlockAsync(string chainId, string blockHash)
    {
        _logger.LogInformation("chainId: {chainId}, blockHash: {blockHash}", chainId, blockHash);
        var requestUrl = GetRequestUrl(Client(chainId).BaseUrl, string.Format("api/blockChain/block?blockHash={0}&includeTransactions={1}", (object) blockHash, false));
        _logger.LogInformation("requestUrl: {requestUrl}", requestUrl);
        return await Client(chainId).GetBlockByHashAsync(blockHash);
    }
    
    private string GetRequestUrl(string baseUrl, string relativeUrl) => new Uri(new Uri(baseUrl + (baseUrl.EndsWith("/") ? "" : "/")), relativeUrl).ToString();

    public async Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId,
        string sender, string contractName, string methodName,
        IMessage param, string contractAddress = null)
    {
        // create raw transaction
        var transaction = await CreateRawTransaction(chainId, sender, contractName, methodName, param, contractAddress);

        var transactionId = HashHelper.ComputeFrom(transaction.ToByteArray());
        var signature = await _signatureProvider.GetTransactionSignature(sender, transactionId);
        AssertHelper.NotEmpty(signature, "Get transaction signature failed, sender={Sender}", sender);
        transaction.Signature = ByteStringHelper.FromHexString(signature);
        return (transactionId, transaction);
    }

    private async Task<Transaction> CreateRawTransaction(string chainId,
        string sender, string contractName, string methodName,
        IMessage param, string contractAddress = null)
    {
        var address = contractAddress ?? await GetContractAddressAsync(chainId, contractName);
        var client = Client(chainId);
        var status = await client.GetChainStatusAsync();

        var prevHeight = status.BestChainHeight - 8;
        var prevBlock = await client.GetBlockByHeightAsync(prevHeight);

        // create raw transaction
        return new Transaction
        {
            From = Address.FromBase58(sender),
            To = Address.FromBase58(address),
            MethodName = methodName,
            Params = param.ToByteString(),
            RefBlockNumber = prevHeight,
            RefBlockPrefix = ByteString.CopyFrom(Hash.LoadFromHex(prevBlock.BlockHash).Value.Take(4).ToArray())
        };
    }

    [ItemCanBeNull]
    public async Task<Tuple<bool, string>> SendTransactionAsync(string chainId, Transaction transaction)
    {
        try
        {
            _logger.LogInformation("Send transaction to {ChainId}, tx: {Tx}", chainId,
                transaction.ToByteArray().ToHex());
            var client = Client(chainId);
            await client.SendTransactionAsync(new SendTransactionInput()
            {
                RawTransaction = transaction.ToByteArray().ToHex()
            });
            return Tuple.Create(true, CommonConstant.EmptyString);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send transaction error to {ChainId}, tx: {Tx}", chainId,
                transaction.ToByteArray().ToHex());
            return Tuple.Create(true, e.Message);
        }
    }

    public async Task<T> CallTransactionAsync<T>(string chainId, string contractName, string methodName,
        IMessage param, string contractAddress = null) where T : class
    {
        var sender = Address.FromPublicKey(_internalKeyPair.PublicKey);
        var transaction = await CreateRawTransaction(chainId, sender.ToBase58(), contractName, methodName, param,contractAddress);
        var transactionId = HashHelper.ComputeFrom(transaction.ToByteArray());
        transaction.Signature = ByteStringHelper.FromHexString(CryptoHelper
            .SignWithPrivateKey(_internalKeyPair.PrivateKey, transactionId.ToByteArray()).ToHex());
        return await CallTransactionAsync<T>(chainId, transaction);
    }

    public async Task<T> CallTransactionAsync<T>(string chainId, Transaction transaction) where T : class
    {
        var client = Client(chainId);
        var rawTransactionResult = await client.ExecuteRawTransactionAsync(new ExecuteRawTransactionDto()
        {
            RawTransaction = transaction.ToByteArray().ToHex(),
            Signature = transaction.Signature.ToHex()
        });

        if (typeof(T) == typeof(string))
        {
            return rawTransactionResult?.Substring(1, rawTransactionResult.Length - 2) as T;
        }

        return (T)JsonConvert.DeserializeObject(rawTransactionResult, typeof(T), _settings);
    }

    /// <summary>
    ///     When the transaction is just sent to the node,
    ///     the query may appear NotExisted status immediately, so this method can help skip this period of time,
    ///     When the transaction pre-verification fails, the Node Invalid Failed status is returned.
    ///     When the transaction becomes NotExisted again, that is, the transaction is rolled back.
    /// </summary>
    /// <param name="chainId"></param>
    /// <param name="transactionId"></param>
    /// <param name="maxWaitMillis"></param>
    /// <param name="delayMillis"></param>
    /// <returns></returns>
    public async Task<TransactionResultDto> WaitTransactionResultAsync(string chainId, string transactionId,
        int maxWaitMillis = 5000, int delayMillis = 1000)
    {
        var waitingStatus = new List<string>
        {
            CommonConstant.TransactionState.NotExisted,
        };
        TransactionResultDto rawTxResult = null;
        using var cts = new CancellationTokenSource(maxWaitMillis);
        try
        {
            while (!cts.IsCancellationRequested && (rawTxResult == null || waitingStatus.Contains(rawTxResult.Status)))
            {
                // delay some times
                await Task.Delay(delayMillis, cts.Token);

                rawTxResult = await QueryTransactionResultAsync(chainId, transactionId);
                _logger.LogDebug(
                    "WaitTransactionResultAsync chainId={ChainId}, transactionId={TransactionId}, status={Status}" +
                    ", error={Error}", chainId, transactionId, rawTxResult.Status, rawTxResult.Error);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Timed out waiting for transactionId {TransactionId} result", transactionId);
        }

        return rawTxResult;
    }
}

public class AddressConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Address);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        return Address.FromBase58(reader.Value as string);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}