using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Awaken.Contracts.Swap;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Options;
using ETransferServer.Swap.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ETransferServer.Swap;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class SwapAppService : ApplicationService, ISwapAppService
{
    private readonly SwapInfosOptions _swapInfosOptions;
    private readonly IContractProvider _contractProvider;
    private readonly IClusterClient _clusterClient;

    public SwapAppService(IOptionsSnapshot<SwapInfosOptions> swapSlippageOptions, IContractProvider contractProvider,
        IClusterClient clusterClient)
    {
        _contractProvider = contractProvider;
        _clusterClient = clusterClient;
        _swapInfosOptions = swapSlippageOptions.Value;
    }

    public decimal GetSlippage(string symbolIn, string symbolOut)
    {
        var swapSymbol = GenerateSwapSymbol(symbolIn, symbolOut);
        return _swapInfosOptions.SwapInfos.TryGetValue(swapSymbol, out var swapInfo) ? swapInfo.Slippage : 0;
    }

    public Task<decimal> GetConversionRate(string chainId, string symbolIn, string symbolOut)
    {
        throw new System.NotImplementedException();
    }

    public async Task<GetAmountsOutDto> CalculateAmountsOut(string chainId, string symbolIn, string symbolOut,
        decimal amountIn)
    {
        var swapSymbol = GenerateSwapSymbol(symbolIn, symbolOut);
        var tokenInfo = await GetAmountActual(symbolIn, chainId);
        var amountInActual = (long)(amountIn * (decimal)Math.Pow(10, tokenInfo.Decimals));

        var result = new GetAmountsOutDto
        {
            SymbolIn = symbolIn,
            SymbolOut = symbolOut,
            AmountIn = amountIn,
            AmountOut = 0
        };
        if (!_swapInfosOptions.SwapInfos.TryGetValue(swapSymbol, out var swapInfo))
        {
            return result;
        }

        try
        {
            var amountsOut = await _contractProvider.CallTransactionAsync<GetAmountsOutOutput>(chainId, null,
                "GetAmountsOut", new GetAmountsOutInput
                {
                    AmountIn = amountInActual,
                    Path = { swapInfo.Path }
                }, swapInfo.Router);
            var tokenOutInfo = await GetAmountActual(symbolIn, chainId);
            var amountOutActual = (long)(amountsOut.Amount.ToList().Last() * (decimal)Math.Pow(10, tokenOutInfo.Decimals));
            result.AmountOut = amountOutActual;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Get amounts out failed.SymbolIn:{symbolIn},SymbolOut:{symbolOut},AmountIn:{amountIn}",
                symbolIn, symbolOut, amountIn);
            return result;
        }

        return result;
    }

    private async Task<TokenDto> GetAmountActual(string symbol,string chainId)
    {
        var tokenGrain =
            _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(symbol, chainId));
        var tokenInfo = await tokenGrain.GetToken();
        AssertHelper.NotNull(tokenInfo, "Token info {symbol}-{chainId} not found", symbol,
            chainId);
        return tokenInfo;
    }

    private static string GenerateSwapSymbol(params string[] symbols)
    {
        return symbols.JoinAsString("-");
    }
}