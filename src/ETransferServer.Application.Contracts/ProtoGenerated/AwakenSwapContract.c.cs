// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: awaken_swap_contract.proto
// </auto-generated>
#pragma warning disable 0414, 1591
#region Designer generated code

using System.Collections.Generic;
using aelf = global::AElf.CSharp.Core;

namespace Awaken.Contracts.Swap {

  #region Events
  internal partial class PairCreated : aelf::IEvent<PairCreated>
  {
    public global::System.Collections.Generic.IEnumerable<PairCreated> GetIndexed()
    {
      return new List<PairCreated>
      {
      };
    }

    public PairCreated GetNonIndexed()
    {
      return new PairCreated
      {
        SymbolA = SymbolA,
        SymbolB = SymbolB,
        Pair = Pair,
      };
    }
  }

  internal partial class LiquidityAdded : aelf::IEvent<LiquidityAdded>
  {
    public global::System.Collections.Generic.IEnumerable<LiquidityAdded> GetIndexed()
    {
      return new List<LiquidityAdded>
      {
      };
    }

    public LiquidityAdded GetNonIndexed()
    {
      return new LiquidityAdded
      {
        Sender = Sender,
        SymbolA = SymbolA,
        SymbolB = SymbolB,
        AmountA = AmountA,
        AmountB = AmountB,
        To = To,
        Pair = Pair,
        LiquidityToken = LiquidityToken,
        Channel = Channel,
      };
    }
  }

  internal partial class LiquidityRemoved : aelf::IEvent<LiquidityRemoved>
  {
    public global::System.Collections.Generic.IEnumerable<LiquidityRemoved> GetIndexed()
    {
      return new List<LiquidityRemoved>
      {
      };
    }

    public LiquidityRemoved GetNonIndexed()
    {
      return new LiquidityRemoved
      {
        Sender = Sender,
        SymbolA = SymbolA,
        SymbolB = SymbolB,
        AmountA = AmountA,
        AmountB = AmountB,
        To = To,
        Pair = Pair,
        LiquidityToken = LiquidityToken,
      };
    }
  }

  internal partial class Swap : aelf::IEvent<Swap>
  {
    public global::System.Collections.Generic.IEnumerable<Swap> GetIndexed()
    {
      return new List<Swap>
      {
      };
    }

    public Swap GetNonIndexed()
    {
      return new Swap
      {
        Sender = Sender,
        SymbolIn = SymbolIn,
        SymbolOut = SymbolOut,
        AmountIn = AmountIn,
        AmountOut = AmountOut,
        TotalFee = TotalFee,
        Pair = Pair,
        To = To,
        Channel = Channel,
      };
    }
  }

  internal partial class Sync : aelf::IEvent<Sync>
  {
    public global::System.Collections.Generic.IEnumerable<Sync> GetIndexed()
    {
      return new List<Sync>
      {
      };
    }

    public Sync GetNonIndexed()
    {
      return new Sync
      {
        SymbolA = SymbolA,
        SymbolB = SymbolB,
        ReserveA = ReserveA,
        ReserveB = ReserveB,
        Pair = Pair,
      };
    }
  }

  #endregion
  internal static partial class AwakenSwapContractContainer
  {
    static readonly string __ServiceName = "AwakenSwapContract";

    #region Marshallers
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.InitializeInput> __Marshaller_InitializeInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.InitializeInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Google.Protobuf.WellKnownTypes.Empty> __Marshaller_google_protobuf_Empty = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.CreatePairInput> __Marshaller_CreatePairInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.CreatePairInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::AElf.Types.Address> __Marshaller_aelf_Address = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::AElf.Types.Address.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.AddLiquidityInput> __Marshaller_AddLiquidityInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.AddLiquidityInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.AddLiquidityOutput> __Marshaller_AddLiquidityOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.AddLiquidityOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.RemoveLiquidityInput> __Marshaller_RemoveLiquidityInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.RemoveLiquidityInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.RemoveLiquidityOutput> __Marshaller_RemoveLiquidityOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.RemoveLiquidityOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.SwapExactTokensForTokensInput> __Marshaller_SwapExactTokensForTokensInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.SwapExactTokensForTokensInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.SwapOutput> __Marshaller_SwapOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.SwapOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.SwapTokensForExactTokensInput> __Marshaller_SwapTokensForExactTokensInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.SwapTokensForExactTokensInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Google.Protobuf.WellKnownTypes.Int64Value> __Marshaller_google_protobuf_Int64Value = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Google.Protobuf.WellKnownTypes.Int64Value.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensInput> __Marshaller_SwapExactTokensForTokensSupportingFeeOnTransferTokensInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput> __Marshaller_SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.StringList> __Marshaller_StringList = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.StringList.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetReservesInput> __Marshaller_GetReservesInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetReservesInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetReservesOutput> __Marshaller_GetReservesOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetReservesOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetTotalSupplyOutput> __Marshaller_GetTotalSupplyOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetTotalSupplyOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountInInput> __Marshaller_GetAmountInInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountInInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountOutInput> __Marshaller_GetAmountOutInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountOutInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.QuoteInput> __Marshaller_QuoteInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.QuoteInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::AElf.Types.BigIntValue> __Marshaller_aelf_BigIntValue = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::AElf.Types.BigIntValue.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetPairAddressInput> __Marshaller_GetPairAddressInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetPairAddressInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountsOutInput> __Marshaller_GetAmountsOutInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountsOutInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountsOutOutput> __Marshaller_GetAmountsOutOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountsOutOutput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountsInInput> __Marshaller_GetAmountsInInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountsInInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Awaken.Contracts.Swap.GetAmountsInOutput> __Marshaller_GetAmountsInOutput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Awaken.Contracts.Swap.GetAmountsInOutput.Parser.ParseFrom);
    #endregion

    #region Methods
    static readonly aelf::Method<global::Awaken.Contracts.Swap.InitializeInput, global::Google.Protobuf.WellKnownTypes.Empty> __Method_Initialize = new aelf::Method<global::Awaken.Contracts.Swap.InitializeInput, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "Initialize",
        __Marshaller_InitializeInput,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.CreatePairInput, global::AElf.Types.Address> __Method_CreatePair = new aelf::Method<global::Awaken.Contracts.Swap.CreatePairInput, global::AElf.Types.Address>(
        aelf::MethodType.Action,
        __ServiceName,
        "CreatePair",
        __Marshaller_CreatePairInput,
        __Marshaller_aelf_Address);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.AddLiquidityInput, global::Awaken.Contracts.Swap.AddLiquidityOutput> __Method_AddLiquidity = new aelf::Method<global::Awaken.Contracts.Swap.AddLiquidityInput, global::Awaken.Contracts.Swap.AddLiquidityOutput>(
        aelf::MethodType.Action,
        __ServiceName,
        "AddLiquidity",
        __Marshaller_AddLiquidityInput,
        __Marshaller_AddLiquidityOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.RemoveLiquidityInput, global::Awaken.Contracts.Swap.RemoveLiquidityOutput> __Method_RemoveLiquidity = new aelf::Method<global::Awaken.Contracts.Swap.RemoveLiquidityInput, global::Awaken.Contracts.Swap.RemoveLiquidityOutput>(
        aelf::MethodType.Action,
        __ServiceName,
        "RemoveLiquidity",
        __Marshaller_RemoveLiquidityInput,
        __Marshaller_RemoveLiquidityOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensInput, global::Awaken.Contracts.Swap.SwapOutput> __Method_SwapExactTokensForTokens = new aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensInput, global::Awaken.Contracts.Swap.SwapOutput>(
        aelf::MethodType.Action,
        __ServiceName,
        "SwapExactTokensForTokens",
        __Marshaller_SwapExactTokensForTokensInput,
        __Marshaller_SwapOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.SwapTokensForExactTokensInput, global::Awaken.Contracts.Swap.SwapOutput> __Method_SwapTokensForExactTokens = new aelf::Method<global::Awaken.Contracts.Swap.SwapTokensForExactTokensInput, global::Awaken.Contracts.Swap.SwapOutput>(
        aelf::MethodType.Action,
        __ServiceName,
        "SwapTokensForExactTokens",
        __Marshaller_SwapTokensForExactTokensInput,
        __Marshaller_SwapOutput);

    static readonly aelf::Method<global::Google.Protobuf.WellKnownTypes.Int64Value, global::Google.Protobuf.WellKnownTypes.Empty> __Method_SetFeeRate = new aelf::Method<global::Google.Protobuf.WellKnownTypes.Int64Value, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "SetFeeRate",
        __Marshaller_google_protobuf_Int64Value,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty> __Method_SetFeeTo = new aelf::Method<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "SetFeeTo",
        __Marshaller_aelf_Address,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensInput, global::Google.Protobuf.WellKnownTypes.Empty> __Method_SwapExactTokensForTokensSupportingFeeOnTransferTokens = new aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensInput, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "SwapExactTokensForTokensSupportingFeeOnTransferTokens",
        __Marshaller_SwapExactTokensForTokensSupportingFeeOnTransferTokensInput,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty> __Method_ChangeOwner = new aelf::Method<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "ChangeOwner",
        __Marshaller_aelf_Address,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput, global::Google.Protobuf.WellKnownTypes.Empty> __Method_SwapExactTokensForTokensSupportingFeeOnTransferTokensVerify = new aelf::Method<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "SwapExactTokensForTokensSupportingFeeOnTransferTokensVerify",
        __Marshaller_SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::Awaken.Contracts.Swap.StringList> __Method_GetPairs = new aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::Awaken.Contracts.Swap.StringList>(
        aelf::MethodType.View,
        __ServiceName,
        "GetPairs",
        __Marshaller_google_protobuf_Empty,
        __Marshaller_StringList);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetReservesInput, global::Awaken.Contracts.Swap.GetReservesOutput> __Method_GetReserves = new aelf::Method<global::Awaken.Contracts.Swap.GetReservesInput, global::Awaken.Contracts.Swap.GetReservesOutput>(
        aelf::MethodType.View,
        __ServiceName,
        "GetReserves",
        __Marshaller_GetReservesInput,
        __Marshaller_GetReservesOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.StringList, global::Awaken.Contracts.Swap.GetTotalSupplyOutput> __Method_GetTotalSupply = new aelf::Method<global::Awaken.Contracts.Swap.StringList, global::Awaken.Contracts.Swap.GetTotalSupplyOutput>(
        aelf::MethodType.View,
        __ServiceName,
        "GetTotalSupply",
        __Marshaller_StringList,
        __Marshaller_GetTotalSupplyOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetAmountInInput, global::Google.Protobuf.WellKnownTypes.Int64Value> __Method_GetAmountIn = new aelf::Method<global::Awaken.Contracts.Swap.GetAmountInInput, global::Google.Protobuf.WellKnownTypes.Int64Value>(
        aelf::MethodType.View,
        __ServiceName,
        "GetAmountIn",
        __Marshaller_GetAmountInInput,
        __Marshaller_google_protobuf_Int64Value);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetAmountOutInput, global::Google.Protobuf.WellKnownTypes.Int64Value> __Method_GetAmountOut = new aelf::Method<global::Awaken.Contracts.Swap.GetAmountOutInput, global::Google.Protobuf.WellKnownTypes.Int64Value>(
        aelf::MethodType.View,
        __ServiceName,
        "GetAmountOut",
        __Marshaller_GetAmountOutInput,
        __Marshaller_google_protobuf_Int64Value);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.QuoteInput, global::Google.Protobuf.WellKnownTypes.Int64Value> __Method_Quote = new aelf::Method<global::Awaken.Contracts.Swap.QuoteInput, global::Google.Protobuf.WellKnownTypes.Int64Value>(
        aelf::MethodType.View,
        __ServiceName,
        "Quote",
        __Marshaller_QuoteInput,
        __Marshaller_google_protobuf_Int64Value);

    static readonly aelf::Method<global::AElf.Types.Address, global::AElf.Types.BigIntValue> __Method_GetKLast = new aelf::Method<global::AElf.Types.Address, global::AElf.Types.BigIntValue>(
        aelf::MethodType.View,
        __ServiceName,
        "GetKLast",
        __Marshaller_aelf_Address,
        __Marshaller_aelf_BigIntValue);

    static readonly aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address> __Method_GetAdmin = new aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address>(
        aelf::MethodType.View,
        __ServiceName,
        "GetAdmin",
        __Marshaller_google_protobuf_Empty,
        __Marshaller_aelf_Address);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetPairAddressInput, global::AElf.Types.Address> __Method_GetPairAddress = new aelf::Method<global::Awaken.Contracts.Swap.GetPairAddressInput, global::AElf.Types.Address>(
        aelf::MethodType.View,
        __ServiceName,
        "GetPairAddress",
        __Marshaller_GetPairAddressInput,
        __Marshaller_aelf_Address);

    static readonly aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address> __Method_GetFeeTo = new aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address>(
        aelf::MethodType.View,
        __ServiceName,
        "GetFeeTo",
        __Marshaller_google_protobuf_Empty,
        __Marshaller_aelf_Address);

    static readonly aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::Google.Protobuf.WellKnownTypes.Int64Value> __Method_GetFeeRate = new aelf::Method<global::Google.Protobuf.WellKnownTypes.Empty, global::Google.Protobuf.WellKnownTypes.Int64Value>(
        aelf::MethodType.View,
        __ServiceName,
        "GetFeeRate",
        __Marshaller_google_protobuf_Empty,
        __Marshaller_google_protobuf_Int64Value);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetAmountsOutInput, global::Awaken.Contracts.Swap.GetAmountsOutOutput> __Method_GetAmountsOut = new aelf::Method<global::Awaken.Contracts.Swap.GetAmountsOutInput, global::Awaken.Contracts.Swap.GetAmountsOutOutput>(
        aelf::MethodType.View,
        __ServiceName,
        "GetAmountsOut",
        __Marshaller_GetAmountsOutInput,
        __Marshaller_GetAmountsOutOutput);

    static readonly aelf::Method<global::Awaken.Contracts.Swap.GetAmountsInInput, global::Awaken.Contracts.Swap.GetAmountsInOutput> __Method_GetAmountsIn = new aelf::Method<global::Awaken.Contracts.Swap.GetAmountsInInput, global::Awaken.Contracts.Swap.GetAmountsInOutput>(
        aelf::MethodType.View,
        __ServiceName,
        "GetAmountsIn",
        __Marshaller_GetAmountsInInput,
        __Marshaller_GetAmountsInOutput);

    #endregion

    #region Descriptors
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::Awaken.Contracts.Swap.AwakenSwapContractReflection.Descriptor.Services[0]; }
    }

    public static global::System.Collections.Generic.IReadOnlyList<global::Google.Protobuf.Reflection.ServiceDescriptor> Descriptors
    {
      get
      {
        return new global::System.Collections.Generic.List<global::Google.Protobuf.Reflection.ServiceDescriptor>()
        {
          global::AElf.Standards.ACS12.Acs12Reflection.Descriptor.Services[0],
          global::Awaken.Contracts.Swap.AwakenSwapContractReflection.Descriptor.Services[0],
        };
      }
    }
    #endregion

    public class AwakenSwapContractReferenceState : global::AElf.Sdk.CSharp.State.ContractReferenceState
    {
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.InitializeInput, global::Google.Protobuf.WellKnownTypes.Empty> Initialize { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.CreatePairInput, global::AElf.Types.Address> CreatePair { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.AddLiquidityInput, global::Awaken.Contracts.Swap.AddLiquidityOutput> AddLiquidity { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.RemoveLiquidityInput, global::Awaken.Contracts.Swap.RemoveLiquidityOutput> RemoveLiquidity { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.SwapExactTokensForTokensInput, global::Awaken.Contracts.Swap.SwapOutput> SwapExactTokensForTokens { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.SwapTokensForExactTokensInput, global::Awaken.Contracts.Swap.SwapOutput> SwapTokensForExactTokens { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Google.Protobuf.WellKnownTypes.Int64Value, global::Google.Protobuf.WellKnownTypes.Empty> SetFeeRate { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty> SetFeeTo { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensInput, global::Google.Protobuf.WellKnownTypes.Empty> SwapExactTokensForTokensSupportingFeeOnTransferTokens { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::AElf.Types.Address, global::Google.Protobuf.WellKnownTypes.Empty> ChangeOwner { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.SwapExactTokensForTokensSupportingFeeOnTransferTokensVerifyInput, global::Google.Protobuf.WellKnownTypes.Empty> SwapExactTokensForTokensSupportingFeeOnTransferTokensVerify { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Google.Protobuf.WellKnownTypes.Empty, global::Awaken.Contracts.Swap.StringList> GetPairs { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetReservesInput, global::Awaken.Contracts.Swap.GetReservesOutput> GetReserves { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.StringList, global::Awaken.Contracts.Swap.GetTotalSupplyOutput> GetTotalSupply { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetAmountInInput, global::Google.Protobuf.WellKnownTypes.Int64Value> GetAmountIn { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetAmountOutInput, global::Google.Protobuf.WellKnownTypes.Int64Value> GetAmountOut { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.QuoteInput, global::Google.Protobuf.WellKnownTypes.Int64Value> Quote { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::AElf.Types.Address, global::AElf.Types.BigIntValue> GetKLast { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address> GetAdmin { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetPairAddressInput, global::AElf.Types.Address> GetPairAddress { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Google.Protobuf.WellKnownTypes.Empty, global::AElf.Types.Address> GetFeeTo { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Google.Protobuf.WellKnownTypes.Empty, global::Google.Protobuf.WellKnownTypes.Int64Value> GetFeeRate { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetAmountsOutInput, global::Awaken.Contracts.Swap.GetAmountsOutOutput> GetAmountsOut { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::Awaken.Contracts.Swap.GetAmountsInInput, global::Awaken.Contracts.Swap.GetAmountsInOutput> GetAmountsIn { get; set; }
    }
  }
}
#endregion
