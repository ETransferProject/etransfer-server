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
using ETransferServer.User;
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
    private readonly INESTRepository<DepositOrder, Guid> _depositOrderIndexRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderDepositAppService> _logger;
    private readonly NetworkOptions _networkInfoOptions;
    private readonly IUserAddressService _userAddressService;
    private readonly INetworkAppService _networkAppService;

    public OrderDepositAppService(INESTRepository<DepositOrder, Guid> depositOrderIndexRepository,
        IObjectMapper objectMapper,
        ILogger<OrderDepositAppService> logger,
        IOptionsSnapshot<NetworkOptions> networkInfoOptions,
        IUserAddressService userAddressService,
        INetworkAppService networkAppService)
    {
        _depositOrderIndexRepository = depositOrderIndexRepository;
        _networkInfoOptions = networkInfoOptions.Value;
        _objectMapper = objectMapper;
        _logger = logger;
        _userAddressService = userAddressService;
        _networkAppService = networkAppService;
    }

    public async Task<GetDepositInfoDto> GetDepositInfoAsync(GetDepositRequestDto request)
    {
        try
        {
            AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
                || request.ChainId == ChainId.tDVW, "Param is invalid. Please refresh and try again.");
            AssertHelper.IsTrue(_networkInfoOptions.NetworkMap.ContainsKey(request.Symbol), 
                "Symbol is not exist. Please refresh and try again.");
            
            var networkConfigs = _networkInfoOptions.NetworkMap[request.Symbol];
            var depositInfo = networkConfigs.Where(n => n.NetworkInfo.Network == request.Network)
                .Select(n => n.DepositInfo).FirstOrDefault();
            AssertHelper.IsTrue(depositInfo != null, "Network is not exist. Please refresh and try again.");

            var getUserDepositAddressInput = new GetUserDepositAddressInput()
            {
                UserId = CurrentUser.GetId().ToString(),
                ChainId = request.ChainId,
                NetWork = request.Network,
                Symbol = request.Symbol
            };

            var getDepositInfoDto = new GetDepositInfoDto();
            var userAddressAsync = await _userAddressService.GetUserAddressAsync(getUserDepositAddressInput);
            getDepositInfoDto.DepositInfo = new DepositInfoDto()
            {
                DepositAddress = userAddressAsync,
                MinAmount = depositInfo.MinDeposit,
                ExtraNotes = depositInfo.ExtraNotes
            };
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
                _objectMapper.Map<List<DepositOrderDto>, List<DepositOrder>>(dtoList));
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
            await _depositOrderIndexRepository.AddOrUpdateAsync(_objectMapper.Map<DepositOrderDto, DepositOrder>(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError("Save depositOrderIndex fail: {id},{message}", dto.Id, ex.Message);
            return false;
        }

        return true;
    }
}