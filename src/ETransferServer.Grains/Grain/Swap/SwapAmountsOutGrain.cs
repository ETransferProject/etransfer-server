using Volo.Abp.ObjectMapping;
using Awaken.Contracts.Swap;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.State.Swap;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Grains.Grain.Swap;

public interface ISwapAmountsOutGrain : IGrainWithStringKey
{
    Task<(decimal, decimal, long, long)> GetAmountsOut(decimal amountIn, List<string> path,
        bool isConversionRate = false);

    public static string GenGrainId(string chainId, string symbolIn, string symbolOut, string router)
    {
        return SwapAmountsGrainId.Of(chainId, symbolIn, symbolOut, router).SwapAmountsOutGrainId();
    }
}

public class SwapAmountsOutGrain : Grain<SwapAmountsState>, ISwapAmountsOutGrain
{
    private readonly ILogger<SwapAmountsOutGrain> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IContractProvider _contractProvider;

    public SwapAmountsOutGrain(IObjectMapper objectMapper, IContractProvider contractProvider,
        ILogger<SwapAmountsOutGrain> logger)
    {
        _objectMapper = objectMapper;
        _contractProvider = contractProvider;
        _logger = logger;
    }

    public async Task<(decimal, decimal, long, long)> GetAmountsOut(decimal amountIn, List<string> path,
        bool isConversionRate = false)
    {
        var grainId = SwapAmountsGrainId.FromGrainId(this.GetPrimaryKeyString());
        if (grainId == null)
        {
            return (0, 0, 0, 0);
        }

        var (_, amountOut, amountInActual, amountOutActual) = await GetAmountsOutAsync(grainId.ChainId,
            grainId.SymbolIn, grainId.SymbolOut,
            amountIn, path, grainId.Router);
        switch (isConversionRate)
        {
            case true when amountOut == 0:
                amountOut = State.ConversionRate;
                break;
            case false:
                return (amountIn, amountOut, amountInActual, amountOutActual);
        }

        State.ConversionRate = amountOut;
        await WriteStateAsync();
        return (amountIn, amountOut, amountInActual, amountOutActual);
    }

    private async Task<(decimal, decimal, long, long)> GetAmountsOutAsync(string chainId, string symbolIn,
        string symbolOut,
        decimal amountIn, List<string> path, string router)
    {
        var tokenInfo = await GetTokenAsync(symbolIn, chainId);
        var amountInActual = (long)(amountIn * (decimal)Math.Pow(10, tokenInfo.Decimals));

        try
        {
            var amountsOut = await _contractProvider.CallTransactionAsync<GetAmountsOutOutput>(chainId, null,
                "GetAmountsOut", new GetAmountsOutInput
                {
                    AmountIn = amountInActual,
                    Path = { path }
                }, router);
            if (amountsOut == null)
            {
                return (0, 0, 0, 0);
            }

            var tokenOutInfo = await GetTokenAsync(symbolOut, chainId);
            var amountOutActual = amountsOut.Amount.ToList().Last();
            var amountOut =
                (amountOutActual / (decimal)Math.Pow(10, tokenOutInfo.Decimals));
            return (amountIn, amountOut, amountInActual, amountOutActual);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get amounts out failed.SymbolIn:{symbolIn},SymbolOut:{symbolOut},Router:{router}",
                symbolIn, symbolOut, router);
            return (0, 0, 0, 0);
        }
    }

    private async Task<TokenDto> GetTokenAsync(string symbol, string chainId)
    {
        var tokenGrain =
            GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(symbol, chainId));
        var tokenInfo = await tokenGrain.GetToken();
        AssertHelper.NotNull(tokenInfo, "Token info {symbol}-{chainId} not found", symbol,
            chainId);
        return tokenInfo;
    }
}