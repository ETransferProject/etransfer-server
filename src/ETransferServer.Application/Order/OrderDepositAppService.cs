using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using ETransferServer.token;
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
public class OrderDepositAppService : ApplicationService, IOrderDepositAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _depositOrderIndexRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderDepositAppService> _logger;
    private readonly IOptionsSnapshot<NetworkOptions> _networkInfoOptions;
    private readonly IUserAddressService _userAddressService;
    private readonly INetworkAppService _networkAppService;
    private readonly ITokenAppService _tokenAppService;
    public OrderDepositAppService(INESTRepository<OrderIndex, Guid> depositOrderIndexRepository,
        IObjectMapper objectMapper,
        ILogger<OrderDepositAppService> logger,
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IUserAddressService userAddressService,
        INetworkAppService networkAppService, ITokenAppService tokenAppService)
    {
        _depositOrderIndexRepository = depositOrderIndexRepository;
        _networkInfoOptions = networkInfoOptions;
        _objectMapper = objectMapper;
        _logger = logger;
        _userAddressService = userAddressService;
        _networkAppService = networkAppService;
        _tokenAppService = tokenAppService;
    }

    public async Task<GetDepositInfoDto> GetDepositInfoAsync(GetDepositRequestDto request)
    {
        try
        {
            AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
                || request.ChainId == ChainId.tDVW, "Param is invalid. Please refresh and try again.");
            AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.Symbol), 
                "Symbol is not exist. Please refresh and try again.");
            AssertHelper.IsTrue(request.ToSymbol.IsNullOrEmpty() || _networkInfoOptions.Value.NetworkMap.ContainsKey(request.ToSymbol), 
                "ToSymbol is not null but does not exist. Please refresh and try again.");
            AssertHelper.IsTrue(
                request.ToSymbol.IsNullOrEmpty() || DepositSwapHelper.NoDepositSwap(request.Symbol, request.ToSymbol) ||
                _tokenAppService.IsValidSwapAsync(request.Symbol, request.ToSymbol),
                "Must be a valid Swap Deposit. Please refresh and try again.");
            
            var networkConfigs = _networkInfoOptions.Value.NetworkMap[request.Symbol];
            var depositInfo = networkConfigs.Where(n => n.NetworkInfo.Network == request.Network)
                .Select(n => n.DepositInfo).FirstOrDefault();
            AssertHelper.IsTrue(depositInfo != null, "Network is not exist. Please refresh and try again.");

            var getUserDepositAddressInput = new GetUserDepositAddressInput()
            {
                UserId = CurrentUser.GetId().ToString(),
                ChainId = request.ChainId,
                NetWork = request.Network,
                Symbol = request.Symbol,
                ToSymbol = request.ToSymbol
            };

            var getDepositInfoDto = new GetDepositInfoDto();
            var userAddressAsync = await _userAddressService.GetUserAddressAsync(getUserDepositAddressInput);
            getDepositInfoDto.DepositInfo = new DepositInfoDto()
            {
                DepositAddress = userAddressAsync,
                MinAmount = depositInfo.MinDeposit,
                ExtraNotes = depositInfo.ExtraNotes,
            };

            if (DepositSwapHelper.IsDepositSwap(request.Symbol, request.ToSymbol))
            {
                getDepositInfoDto.DepositInfo.ExtraNotes = depositInfo.SwapExtraNotes;
                
                getDepositInfoDto.DepositInfo.ExtraInfo = new ExtraInfo();
                // raymond.zhang: set slippage
                getDepositInfoDto.DepositInfo.ExtraInfo.Slippage = Convert.ToDecimal(0.05);
            }

            try
            {
                var avgExchange =
                    await _networkAppService.GetAvgExchangeAsync(request.Symbol, CommonConstant.Symbol.USD);
                getDepositInfoDto.DepositInfo.MinAmountUsd =
                    (depositInfo.MinDeposit.SafeToDecimal() * avgExchange).ToString(
                        DecimalHelper.GetDecimals(request.Symbol),
                        DecimalHelper.RoundingOption.Ceiling);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Get deposit avg exchange failed.");
            }

            return getDepositInfoDto;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetDepositInfo error");
            throw;
        }
    }


    public async Task<bool> BulkAddOrUpdateAsync(List<DepositOrderDto> dtoList)
    {
        try
        {
            await _depositOrderIndexRepository.BulkAddOrUpdateAsync(
                _objectMapper.Map<List<DepositOrderDto>, List<OrderIndex>>(dtoList));
        }
        catch (Exception ex)
        {
            _logger.LogError("Bulk save depositOrderIndex fail: {Count},{Message}", dtoList.Count, ex.Message);
            return false;
        }

        return true;
    }

    public async Task<bool> AddOrUpdateAsync(DepositOrderDto dto)
    {
        try
        {
            await _depositOrderIndexRepository.AddOrUpdateAsync(_objectMapper.Map<DepositOrderDto, OrderIndex>(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError("Save depositOrderIndex fail: {id},{message}", dto.Id, ex.Message);
            return false;
        }

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
        // raymond.zhang: calculate deposit rate
        CalculateDepositRateDto calculateDepositRateDto = new CalculateDepositRateDto();
        calculateDepositRateDto.ConversionRate = new ConversionRate();
        calculateDepositRateDto.ConversionRate.FromSymbol = "USDT";
        calculateDepositRateDto.ConversionRate.ToSymbol = "SGR-1";
        calculateDepositRateDto.ConversionRate.FromAmount = Convert.ToDecimal(1.00);
        calculateDepositRateDto.ConversionRate.ToAmount = Convert.ToDecimal(0.88);
        calculateDepositRateDto.ConversionRate.MinimumReceiveAmount = Convert.ToDecimal(0.80);
        return calculateDepositRateDto;
    }
}