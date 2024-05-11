using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Grains.Grain.Swap;
using ETransferServer.Options;
using ETransferServer.Swap.Dtos;
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
    private readonly IClusterClient _clusterClient;

    public SwapAppService(IOptionsSnapshot<SwapInfosOptions> swapInfosOptions,
        IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
        _swapInfosOptions = swapInfosOptions.Value;
    }

    public decimal GetSlippage(string symbolIn, string symbolOut)
    {
        var swapSymbol = GeneratePairSymbol(symbolIn, symbolOut);
        return _swapInfosOptions.PairInfos.TryGetValue(swapSymbol, out var swapInfo) ? swapInfo.Slippage : 0;
    }

    public async Task<decimal> GetConversionRate(string chainId, string symbolIn, string symbolOut)
    {
        var swapSymbol = GeneratePairSymbol(symbolIn, symbolOut);
        if (!_swapInfosOptions.PairInfos.TryGetValue(swapSymbol, out var swapInfo))
        {
            return 0;
        }

        var swapAmountsGrain =
            _clusterClient.GetGrain<ISwapAmountsOutGrain>(
                ISwapAmountsOutGrain.GenGrainId(chainId, symbolIn, symbolOut, swapInfo.Router));
        var (_, amountOut,_,_) = await swapAmountsGrain.GetAmountsOutAsync(1, swapInfo.Path, true);
        return amountOut;
    }

    public async Task<GetAmountsOutDto> CalculateAmountsOut(string chainId, string symbolIn, string symbolOut,
        decimal amountIn)
    {
        var pairSymbol = GeneratePairSymbol(symbolIn, symbolOut);

        var result = new GetAmountsOutDto
        {
            SymbolIn = symbolIn,
            SymbolOut = symbolOut,
            AmountIn = amountIn,
            AmountOut = 0,
            MinAmountOut = 0
        };
        if (!_swapInfosOptions.PairInfos.TryGetValue(pairSymbol, out var swapInfo))
        {
            return result;
        }

        var swapAmountsGrain =
            _clusterClient.GetGrain<ISwapAmountsOutGrain>(
                ISwapAmountsOutGrain.GenGrainId(chainId, symbolIn, symbolOut, swapInfo.Router));
        var (_, amountOut,_,_) = await swapAmountsGrain.GetAmountsOutAsync(amountIn, swapInfo.Path);
        result.AmountOut = amountOut;
        result.MinAmountOut = GetMinOutAmount(amountOut, symbolIn, symbolOut);
        return result;
    }

    // public async Task TestSwap()
    // {
    //     var orderId = Guid.NewGuid();
    //     var swapGrain =
    //         _clusterClient.GetGrain<ISwapGrain>(orderId);
    // }

    private decimal GetMinOutAmount(decimal amountOut, string symbolIn, string symbolOut)
    {
        var slippage = GetSlippage(symbolIn, symbolOut);
        return slippage == 0 ? amountOut : amountOut * (1 - slippage);
    }

    private static string GeneratePairSymbol(params string[] symbols)
    {
        return symbols.JoinAsString("-");
    }
}