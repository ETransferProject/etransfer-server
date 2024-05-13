using AElf.Types;
using Awaken.Contracts.Swap;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using Orleans;
using ETransferServer.Grains.State.Swap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Swap;

public interface ISwapReserveGrain : IGrainWithStringKey
{
    Task<GrainResultDto<ReserveDto>> GetReserveAsync(long? timeStamp, int skipCount, int maxResultCount);

    public static string GenGrainId(string chainId, string symbolIn, string symbolOut, string router)
    {
        return SwapAmountsGrainId.Of(chainId, symbolIn, symbolOut, router).SwapReserveGrainId();
    }
}

public class SwapReserveGrain : Grain<SwapReserveState>, ISwapReserveGrain
{
    private readonly ISwapReserveProvider _swapReserveProvider;
    private readonly ILogger<SwapReserveGrain> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly SwapInfosOptions _swapInfosOptions;
    private readonly IObjectMapper _objectMapper;

    public SwapReserveGrain(ILogger<SwapReserveGrain> logger, ISwapReserveProvider swapReserveProvider,
        IContractProvider contractProvider, IOptionsSnapshot<SwapInfosOptions> swapInfosOptions,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _swapReserveProvider = swapReserveProvider;
        _contractProvider = contractProvider;
        _objectMapper = objectMapper;
        _swapInfosOptions = swapInfosOptions.Value;
    }

    public async Task<GrainResultDto<ReserveDto>> GetReserveAsync(long? timeStamp, int skipCount, int maxResultCount)
    {
        var result = new GrainResultDto<ReserveDto>();
        var grainId = SwapAmountsGrainId.FromGrainId(this.GetPrimaryKeyString());
        var pairAddress = await GetPairAddressAsync();
        if (pairAddress == null)
        {
            result.Success = false;
            result.Message = "Failed to get pair address";
            return result;
        }

        if (await CheckLibHeightAndTimestampAsync(grainId.ChainId, timeStamp))
        {
            var reserve =
                await _swapReserveProvider.GetReserveAsync(grainId.ChainId, pairAddress, timeStamp, skipCount,
                    maxResultCount);
            if (reserve.TotalCount > 0)
            {
                State = _objectMapper.Map<ReserveDto, SwapReserveState>(reserve.Items[0]);
                await WriteStateAsync();
                result.Success = true;
                result.Data = reserve.Items[0];
            }
            else
            {
                result.Success = false;
                result.Message = "Empty reserve list.";
            }

            return result;
        }

        result.Success = false;
        result.Message = "Failed to check lib height and timestamp.";
        return result;
    }

    private async Task<string> GetPairAddressAsync()
    {
        var grainId = SwapAmountsGrainId.FromGrainId(this.GetPrimaryKeyString());
        if (!State.PairAddress.IsNullOrEmpty())
        {
            return State.PairAddress;
        }

        var pairAddress =
            await GetPairAddressAsync(grainId.ChainId, grainId.Router, grainId.SymbolIn, grainId.SymbolOut);
        if (pairAddress.IsNullOrEmpty())
        {
            return State.PairAddress;
        }

        State.PairAddress = pairAddress;
        await WriteStateAsync();
        return State.PairAddress;
    }

    private async Task<string> GetPairAddressAsync(string chainId, string router, string symbolIn, string symbolOut,
        int retryTime = 0)
    {
        if (retryTime > _swapInfosOptions.CallTxRetryTimes)
        {
            _logger.LogError("Get pair address failed after retry {times}.{grainId}",
                _swapInfosOptions.CallTxRetryTimes,
                this.GetPrimaryKeyString());
            return null;
        }

        try
        {
            _logger.LogInformation("Get pair address from chain.{grainId}", this.GetPrimaryKeyString());
            var pairAddress = await _contractProvider.CallTransactionAsync<Address>(chainId, null, "GetPairAddress",
                new GetPairAddressInput
                {
                    SymbolA = symbolIn,
                    SymbolB = symbolOut
                }, router);
            return pairAddress.ToBase58();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get pair address.{chainId}-{symbolIn}-{symbolOut}", chainId, symbolIn,
                symbolOut);
            retryTime += 1;
            await GetPairAddressAsync(chainId, router, symbolIn, symbolOut, retryTime);
        }

        return null;
    }

    private async Task<bool> CheckLibHeightAndTimestampAsync(string chainId, long? timestamp, int retryTime = 0)
    {
        if (retryTime > _swapInfosOptions.CallTxRetryTimes)
        {
            _logger.LogWarning("Get lib height failed after retry {times}.{grainId}",
                _swapInfosOptions.CallTxRetryTimes,
                this.GetPrimaryKeyString());
            return false;
        }

        try
        {
            var libFromGql = await _swapReserveProvider.GetConfirmedHeightAsync(chainId);
            _logger.LogInformation("Get lib from swap gql:{lib},{grainId}", libFromGql, this.GetPrimaryKeyString());
            var chainStatus = await _contractProvider.GetChainStatusAsync(chainId);
            var libFromChain = chainStatus.LastIrreversibleBlockHeight;
            _logger.LogInformation("Get lib from chain:{lib},{grainId}", libFromChain, this.GetPrimaryKeyString());
            var blockTime = (await _contractProvider.GetBlockAsync(chainId, chainStatus.LongestChainHeight)).Header
                .Time.ToUtcMilliSeconds();
            _logger.LogInformation("Get block time from chain:{time},{grainId}", blockTime, this.GetPrimaryKeyString());
            return blockTime > timestamp && (libFromGql >= libFromChain ||
                                             libFromGql >= libFromChain - _swapInfosOptions.SafeLibDiff);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get lib height or block time failed.{grainId}", this.GetPrimaryKeyString());
            retryTime += 1;
            await CheckLibHeightAndTimestampAsync(chainId, retryTime);
        }

        return false;
    }
}