using ETransferServer.Common;

namespace ETransferServer.Grains.Grain.Swap;

public class SwapAmountsGrainId
{
    public string ChainId { get; set; }
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
    public string Router { get; set; }

    public SwapAmountsGrainId(string chainId, string symbolIn, string symbolOut, string router)
    {
        ChainId = chainId;
        SymbolIn = symbolIn;
        SymbolOut = symbolOut;
        Router = router;
    }

    public string SwapAmountsOutGrainId()
    {
        return string.Join(CommonConstant.Underline,ChainId, SymbolIn, SymbolOut, Router, "SwapAmountsOut");
    }

    public string SwapReserveGrainId()
    {
        return string.Join(CommonConstant.Underline, ChainId, SymbolIn, SymbolOut, Router,"SwapReserve");
    }

    public static SwapAmountsGrainId Of(string chainId, string symbolIn, string symbolOut, string router)
    {
        return new SwapAmountsGrainId(chainId, symbolIn, symbolOut, router);
    }

    public static SwapAmountsGrainId FromGrainId(string grainId)
    {
        var values = grainId.Split(CommonConstant.Underline);
        if (values.Length < 5 || values[0].IsNullOrEmpty() || values[1].IsNullOrEmpty() || values[2].IsNullOrEmpty() ||
            values[3].IsNullOrEmpty())
        {
            return null;
        }

        return new SwapAmountsGrainId(values[0], values[1], values[2], values[3]);
    }
}