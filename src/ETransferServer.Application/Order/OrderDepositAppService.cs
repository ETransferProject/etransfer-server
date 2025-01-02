using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Deposit.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Orders;
using ETransferServer.Swap;
using ETransferServer.Token;
using ETransferServer.User;
using Nest;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.Order;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class OrderDepositAppService : ApplicationService, IOrderDepositAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _depositOrderIndexRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderDepositAppService> _logger;
    private readonly IOptionsSnapshot<NetworkOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IUserAddressService _userAddressService;
    private readonly INetworkAppService _networkAppService;
    private readonly ITokenAppService _tokenAppService;
    private readonly ISwapAppService _swapAppService;

    public OrderDepositAppService(INESTRepository<OrderIndex, Guid> depositOrderIndexRepository,
        IObjectMapper objectMapper,
        ILogger<OrderDepositAppService> logger,
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IOptionsSnapshot<ChainOptions> chainOptions,
        IUserAddressService userAddressService,
        INetworkAppService networkAppService, ITokenAppService tokenAppService, ISwapAppService swapAppService)
    {
        _depositOrderIndexRepository = depositOrderIndexRepository;
        _networkInfoOptions = networkInfoOptions;
        _chainOptions = chainOptions;
        _objectMapper = objectMapper;
        _logger = logger;
        _userAddressService = userAddressService;
        _networkAppService = networkAppService;
        _tokenAppService = tokenAppService;
        _swapAppService = swapAppService;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderDepositAppService),
        MethodName = nameof(HandleGetInfoExceptionAsync))]
    public async Task<GetDepositInfoDto> GetDepositInfoAsync(GetDepositRequestDto request)
    {
        AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
            || request.ChainId == ChainId.tDVW, "Param is invalid. Please refresh and try again.");
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.Symbol), 
            "Symbol is not exist. Please refresh and try again.");
        AssertHelper.IsTrue(
            request.ToSymbol.IsNullOrEmpty() || 
            _tokenAppService.IsValidDeposit(request.ChainId, request.Symbol, request.ToSymbol),
            "The combination of ChainId, FromSymbol and ToSymbol is an invalid parameter. Please refresh and try again.");
        
        var networkConfigs = _networkInfoOptions.Value.NetworkMap[request.Symbol];
        var depositInfo = networkConfigs.Where(n => n.NetworkInfo.Network == request.Network)
            .Select(n => n.DepositInfo).FirstOrDefault();
        AssertHelper.IsTrue(depositInfo != null, "Network is not exist. Please refresh and try again.");

        var getUserDepositAddressInput = new GetUserDepositAddressInput
        {
            UserId = CurrentUser.GetId().ToString(),
            ChainId = request.ChainId,
            NetWork = request.Network,
            Symbol = request.Symbol,
            ToSymbol = request.ToSymbol
        };

        var getDepositInfoDto = new GetDepositInfoDto();
        var userAddressAsync = await _userAddressService.GetUserAddressAsync(getUserDepositAddressInput);
        var (isOpen, amountThreshold, serviceFee, minAmount) = 
            await _networkAppService.GetServiceFeeAsync(string.Empty, request.Symbol);
        var maxFee = await _networkAppService.GetMaxThirdPartFeeAsync(request.Network, request.Symbol);
        getDepositInfoDto.DepositInfo = new DepositInfoDto
        {
            DepositAddress = userAddressAsync,
            ServiceFee = maxFee.ToString(),
            MinAmount = minAmount.ToString(),
            CurrentThreshold = amountThreshold.ToString(),
            ExtraNotes = depositInfo.ExtraNotes
        };

        if (DepositSwapHelper.IsDepositSwap(request.Symbol, request.ToSymbol))
        {
            getDepositInfoDto.DepositInfo.ExtraNotes = depositInfo.SwapExtraNotes;
            getDepositInfoDto.DepositInfo.ExtraInfo = new ExtraInfo
            {
                Slippage = _swapAppService.GetSlippage(request.Symbol, request.ToSymbol)
            };
        }

        try
        {
            var avgExchange =
                await _networkAppService.GetAvgExchangeAsync(request.Symbol, CommonConstant.Symbol.USD);
            var decimals =
                await _networkAppService.GetDecimalsAsync(request.ChainId, request.Symbol);
            getDepositInfoDto.DepositInfo.ServiceFeeUsd = (maxFee * avgExchange).ToString(
                decimals, DecimalHelper.RoundingOption.Ceiling);
            getDepositInfoDto.DepositInfo.MinAmountUsd =
                (minAmount * avgExchange).ToString(
                    decimals, DecimalHelper.RoundingOption.Ceiling);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get deposit avg exchange failed.");
        }

        return getDepositInfoDto;
    }

    private async Task<int> GetToSymbolDecimalsAsync(String chainId, string fromSymbol, string toSymbol)
    {
        var tokenOptionList = await _tokenAppService.GetTokenOptionListAsync(new GetTokenOptionListRequestDto(){Type = OrderTypeEnum.Deposit.ToString()});

        var tokenOption = tokenOptionList.TokenList.FirstOrDefault(option => option.Symbol == fromSymbol);
        if (tokenOption != null)
        {
            var toTokenOption = tokenOption.ToTokenList.FirstOrDefault(option => option.ChainIdList.Contains(chainId) && option.Symbol == toSymbol);
            if (toTokenOption != null)
            {
                return toTokenOption.Decimals;
            }
        }

        return 8;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderDepositAppService),
        MethodName = nameof(HandleBulkExceptionAsync))]
    public async Task<bool> BulkAddOrUpdateAsync(List<DepositOrderDto> dtoList)
    {
        await _depositOrderIndexRepository.BulkAddOrUpdateAsync(
            _objectMapper.Map<List<DepositOrderDto>, List<OrderIndex>>(dtoList));
        return true;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderDepositAppService),
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<bool> AddOrUpdateAsync(DepositOrderDto dto)
    {
        await _depositOrderIndexRepository.AddOrUpdateAsync(_objectMapper.Map<DepositOrderDto, OrderIndex>(dto));
        return true;
    }
    
    public async Task<bool> ExistSync(DepositOrderDto dto)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.ThirdPartOrderId).Value(dto.ThirdPartOrderId)));

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResponse = await _depositOrderIndexRepository.CountAsync(Filter);
        return countResponse.Count > 0;
    }

    public async Task<CalculateDepositRateDto> CalculateDepositRateAsync(GetCalculateDepositRateRequestDto request)
    {
        AssertHelper.IsTrue(request.ToChainId == ChainId.AELF || request.ToChainId == ChainId.tDVV
                            || request.ToChainId == ChainId.tDVW, "Param is invalid. Please refresh and try again.");
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.FromSymbol), 
            "FromSymbol is not exist. Please refresh and try again.");
        AssertHelper.IsTrue(DepositSwapAmountHelper.IsValidRange(request.FromAmount), "FromAmount is an invalid parameter. Please refresh and try again. ");
        AssertHelper.IsTrue(_tokenAppService.IsValidSwap(request.ToChainId, request.FromSymbol, request.ToSymbol), "The combination of ChainId, FromSymbol and ToSymbol is an invalid parameter. Please refresh and try again.");

        var (isOpen, amountThreshold, serviceFee, minAmount) =
            await _networkAppService.GetServiceFeeAsync(request.FromChainId, request.FromSymbol);
        var fee = await _networkAppService.GetMaxThirdPartFeeAsync(string.Empty, request.FromSymbol);
        var fromAmount = isOpen && request.FromAmount >= minAmount && request.FromAmount < amountThreshold
            ? request.FromAmount - (request.FromChainId.IsNullOrEmpty() ? fee : serviceFee)
            : (!isOpen && request.FromAmount >= minAmount) || (isOpen && request.FromAmount >= amountThreshold)
                ? request.FromAmount
                : 0M;
        request.FromAmount = fromAmount < 0 ? 0M : fromAmount;

        if (request.FromAmount == DepositSwapAmountHelper.AmountZero)
        {
            return new CalculateDepositRateDto
            {
                ConversionRate = new ConversionRate
                {
                    FromSymbol = request.FromSymbol,
                    ToSymbol = request.ToSymbol,
                    FromAmount = request.FromAmount,
                    ToAmount = DepositSwapAmountHelper.AmountZero,
                    MinimumReceiveAmount = DepositSwapAmountHelper.AmountZero,
                    ExtraInfo = new ExtraInfo
                    {
                        Slippage = _swapAppService.GetSlippage(request.FromSymbol, request.ToSymbol)
                    }
                }
            };
        }

        if (request.ToChainId == ChainId.AELF)
            request.ToChainId = _chainOptions.Value.ChainInfos.Keys.FirstOrDefault(t => t != ChainId.AELF);
        var calculateAmountsOut = await _swapAppService.CalculateAmountsOut(request.ToChainId, request.FromSymbol, request.ToSymbol, request.FromAmount);
        return new CalculateDepositRateDto
        {
            ConversionRate = new ConversionRate
            {
                FromSymbol = request.FromSymbol,
                ToSymbol = request.ToSymbol,
                FromAmount = request.FromAmount,
                ToAmount = Math.Round(calculateAmountsOut.AmountOut, await GetToSymbolDecimalsAsync(request.ToChainId, request.FromSymbol, request.ToSymbol)),
                MinimumReceiveAmount = Math.Round(calculateAmountsOut.MinAmountOut, await GetToSymbolDecimalsAsync(request.ToChainId, request.FromSymbol, request.ToSymbol)),
                ExtraInfo = new ExtraInfo
                {
                    Slippage = _swapAppService.GetSlippage(request.FromSymbol, request.ToSymbol)
                }
            }
        };
    }
}