using System;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.ChainsClient.Solana.Helper;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Common.HttpClient;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Solana;

public class SolanaClientProvider : IBlockchainClientProvider
{
    private readonly ILogger<SolanaClientProvider> _logger;
    private readonly BlockChainInfoOptions _blockChainInfoOptions;
    private readonly IHttpProvider _httpProvider;
    private const string Method = "getTransaction";
    private readonly IdGenerator _idGenerator = new();

    public SolanaClientProvider(
        ILogger<SolanaClientProvider> logger, IOptionsSnapshot<BlockChainInfoOptions> blockChainInfoOptions,
        IHttpProvider httpProvider)
    {
        _logger = logger;
        _blockChainInfoOptions = blockChainInfoOptions.Value;
        _httpProvider = httpProvider;
    }

    public BlockchainType ChainType { get; } = BlockchainType.Solana;

    [ExceptionHandler(typeof(Exception), TargetType = typeof(SolanaClientProvider), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        AssertHelper.NotNull(txId, "TxId can not be null.");
        var result = new BlockDtos();
        var param = Parameters.Create(txId,
            ConfigObject.Create(KeyValue.Create("encoding", "json"),
                KeyValue.Create("commitment", Commitment.Finalized),
                KeyValue.Create("maxSupportedTransactionVersion", 0)));
        var parameters = new JsonRpcRequest(_idGenerator.GetNextId(), Method, param);
        var requestJson = JsonConvert.SerializeObject(parameters, HttpProvider.DefaultJsonSettings);
        var res = await _httpProvider.InvokeAsync<SolanaResponse>(HttpMethod.Post,
            _blockChainInfoOptions.ChainInfos[chainId].Api,
            body: requestJson);
        _logger.LogInformation("Result from solana:{res}", JsonConvert.SerializeObject(res));
        var blockTime = res.Result.BlockTime;
        result.BlockTimeStamp = blockTime;
        return result;
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, string chainId, string blockHash, string txId)
    {
        _logger.LogError(ex, "Failed to get solana transaction info, {blockHash},{txId}", blockHash, txId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new BlockDtos()
        };
    }
}