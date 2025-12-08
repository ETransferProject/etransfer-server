using Awaken.Contracts.Swap;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Swap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IContractProvider _contractProvider;
    private readonly SwapInfosOptions _swapInfosOptions;

    public SwapAmountsOutGrain(IContractProvider contractProvider,
        ILogger<SwapAmountsOutGrain> logger,
        IOptionsSnapshot<SwapInfosOptions> swapInfosOptions)
    {
        _contractProvider = contractProvider;
        _logger = logger;
        _swapInfosOptions = swapInfosOptions.Value;
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
            var timeout = TimeSpan.FromSeconds(_swapInfosOptions.TimeOut);
            var timeoutTask = Task.Delay(timeout);
            _logger.LogInformation("GetAmountsOutAsync start, timeOut:{timeOut}", _swapInfosOptions.TimeOut);
            
            var amountsOutTask = _contractProvider.CallTransactionAsync<GetAmountsOutOutput>(chainId, null,
                "GetAmountsOut", new GetAmountsOutInput
                {
                    AmountIn = amountInActual,
                    Path = { path }
                }, router);
            
            var completedTask = await Task.WhenAny(amountsOutTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                _logger.LogInformation("GetAmountsOutAsync cancel");
                return (0, 0, 0, 0);
            }
            var amountsOut = await amountsOutTask;
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