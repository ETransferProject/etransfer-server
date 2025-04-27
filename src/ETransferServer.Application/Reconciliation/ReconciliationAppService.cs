using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Grains.Grain.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Order;
using ETransferServer.Orders;
using ETransferServer.Service.Info;
using ETransferServer.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using Nest;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.Reconciliation;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class ReconciliationAppService : ApplicationService, IReconciliationAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly INESTRepository<TokenPoolIndex, Guid> _tokenPoolIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;
    private readonly IInfoAppService _infoService;
    private readonly IOrderAppService _orderService;
    private readonly INetworkAppService _networkAppService;
    private readonly IdentityUserManager _userManager;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IOptionsSnapshot<StringEncryptionOptions> _stringEncryptionOptions;
    private readonly Dictionary<string, string> MappingItems = new();

    public ReconciliationAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        INESTRepository<TokenPoolIndex, Guid> tokenPoolIndexRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger,
        IInfoAppService infoService,
        IOrderAppService orderService,
        INetworkAppService networkAppService,
        IdentityUserManager userManager,
        IOptionsSnapshot<TokenOptions> tokenOptions,
        IOptionsSnapshot<StringEncryptionOptions> stringEncryptionOptions)
    {
        _orderIndexRepository = orderIndexRepository;
        _tokenPoolIndexRepository = tokenPoolIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _infoService = infoService;
        _orderService = orderService;
        _networkAppService = networkAppService;
        _userManager = userManager;
        _tokenOptions = tokenOptions;
        _stringEncryptionOptions = stringEncryptionOptions;
    }

    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        return await _infoService.GetNetworkOptionAsync();
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleChangePwdExceptionAsync))]
    public async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
        {
            throw new UserFriendlyException("Invalid token.");
        }

        var identityUser = await _userManager.FindByIdAsync(userId.ToString());
        if (identityUser == null)
        {
            throw new UserFriendlyException("Invalid user.");
        }

        var password = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(request.NewPassword));
        if (!VerifyHelper.VerifyPassword(password))
        {
            throw new UserFriendlyException("Invalid password.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
        var result = await _userManager.ResetPasswordAsync(identityUser, token, password);
        if (!result.Succeeded)
        {
            throw new UserFriendlyException(string.Join(",", result.Errors.Select(e => e.Description)));
        }

        return true;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleInitUserExceptionAsync))]
    public async Task<bool> InitUserAsync(GetUserDto request)
    {
        var userGrain = _clusterClient.GetGrain<IUserReconciliationGrain>(GuidHelper.UniqGuid(request.Name));
        await userGrain.AddOrUpdateUser(new UserReconciliationDto
        {
            UserName = request.Name,
            Address = request.Address,
            PasswordHash =
                EncryptionHelper.Encrypt(request.Password, _stringEncryptionOptions.Value.DefaultPassPhrase)
        });
        return true;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleGetDetailExceptionAsync))]
    public async Task<OrderMoreDetailDto> GetOrderRecordDetailAsync(string id)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (id.IsNullOrWhiteSpace() || !userId.HasValue || userId == Guid.Empty) return new OrderMoreDetailDto();

        var (orderDetailDto, orderIndex) = await _orderService.GetOrderDetailAsync(id, true);
        var result = _objectMapper.Map<OrderDetailDto, OrderMoreDetailDto>(orderDetailDto);
        result.RelatedOrderId = !orderIndex.ExtensionInfo.IsNullOrEmpty() &&
                                orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.RelatedOrderId)
            ? orderIndex.ExtensionInfo[ExtensionKey.RelatedOrderId]
            : null;
        result.ThirdPartFee = !orderIndex.ThirdPartFee.IsNullOrEmpty() &&
                              orderIndex.ThirdPartFee[0].Decimals.SafeToInt() > 0
            ? new FeeInfo
            {
                Symbol = orderIndex.ThirdPartFee[0].Symbol.Split(CommonConstant.Underline).LastOrDefault(),
                Amount = (orderIndex.ThirdPartFee[0].Amount.SafeToDecimal() /
                          (decimal)Math.Pow(10, orderIndex.ThirdPartFee[0].Decimals.SafeToInt())).ToString()
            }
            : new FeeInfo();
        result.RoleType = await GetRoleTypeAsync();
        result.OperationStatus = !orderIndex.ExtensionInfo.IsNullOrEmpty() &&
                                 orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
            ? orderIndex.ExtensionInfo[ExtensionKey.SubStatus]
            : null;
        result.Applicant = !orderIndex.ExtensionInfo.IsNullOrEmpty() && 
                           orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.RequestUser)
            ? orderIndex.ExtensionInfo[ExtensionKey.RequestUser]
            : null;
        return result;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleGetListExceptionAsync))]
    public async Task<OrderPagedResultDto<OrderRecordDto>> GetDepositOrderRecordListAsync(GetOrderRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty) return new OrderPagedResultDto<OrderRecordDto>();

        var (count, list) = await GetOrderRecordListAsync(request, OrderTypeEnum.Deposit.ToString());

        return new OrderPagedResultDto<OrderRecordDto>
        {
            TotalAmount = await QuerySumAggAsync(request, OrderTypeEnum.Deposit.ToString()),
            Items = await LoopCollectionItemsAsync(
                _objectMapper.Map<List<OrderIndex>, List<OrderRecordDto>>(list), list),
            TotalCount = count
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleGetWithdrawListExceptionAsync))]
    public async Task<OrderPagedResultDto<OrderMoreDetailDto>> GetWithdrawOrderRecordListAsync(GetOrderRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty) return new OrderPagedResultDto<OrderMoreDetailDto>();

        var (count, list) = await GetOrderRecordListAsync(request, OrderTypeEnum.Withdraw.ToString());

        return new OrderPagedResultDto<OrderMoreDetailDto>
        {
            TotalAmount = await QuerySumAggAsync(request, OrderTypeEnum.Withdraw.ToString()),
            Items = await LoopWithdrawItemsAsync(await LoopCollectionItemsAsync(
                _objectMapper.Map<List<OrderIndex>, List<OrderRecordDto>>(list), list), list),
            TotalCount = count
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleGetFailListExceptionAsync))]
    public async Task<PagedResultDto<OrderRecordDto>> GetFailOrderRecordListAsync(GetOrderRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty) return new PagedResultDto<OrderRecordDto>();

        var (count, list) = await GetOrderRecordListAsync(request, OrderStatusResponseEnum.Failed.ToString());

        return new PagedResultDto<OrderRecordDto>
        {
            Items = await LoopCollectionItemsAsync(
                _objectMapper.Map<List<OrderIndex>, List<OrderRecordDto>>(list), list,
                OrderStatusResponseEnum.Failed.ToString()),
            TotalCount = count
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRequestReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> RequestReleaseTokenAsync(GetRequestReleaseDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Deposit.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        var amount = orderIndex.ToTransfer.Amount.ToString(await _networkAppService.GetDecimalsAsync(ChainId.AELF, 
            orderIndex.ToTransfer.Symbol), DecimalHelper.RoundingOption.Floor);
        if (orderIndex.ToTransfer.ToAddress != request.ToAddress
            || amount.SafeToDecimal() != request.Amount.SafeToDecimal()
            || orderIndex.ToTransfer.Symbol != request.Symbol
            || orderIndex.ToTransfer.ChainId != request.ChainId)
        {
            throw new UserFriendlyException("Invalid param.");
        }

        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus) &&
            (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseRequested.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseConfirming.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseConfirmed.ToString()))
        {
            throw new UserFriendlyException("Invalid request.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseRequested.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserDepositRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.GetAsync()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.CreateOrUpdateAsync(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseRequested.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRejectReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> RejectReleaseTokenAsync(GetOrderOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.ExtensionInfo.IsNullOrEmpty()
                               || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
                               || orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                               OrderOperationStatusEnum.ReleaseRequested.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseRejected.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserDepositRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.GetAsync()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.CreateOrUpdateAsync(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseRejected.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> ReleaseTokenAsync(GetOrderSafeOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        await VerifyCode(request.Code);

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Deposit.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        if (orderIndex.ExtensionInfo.IsNullOrEmpty()
            || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
            || (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.ReleaseRequested.ToString()
                && orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.ReleaseFailed.ToString()))
        {
            throw new UserFriendlyException("Invalid release status.");
        }

        orderIndex.Status = OrderStatusEnum.FromTransferConfirmed.ToString();
        orderIndex.ToTransfer.Status = OrderTransferStatusEnum.Created.ToString();
        if (orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.IsSwap) &&
            orderIndex.ExtensionInfo[ExtensionKey.IsSwap].Equals(Boolean.TrueString))
        {
            orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.NeedSwap, Boolean.TrueString);
            orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SwapStage, SwapStage.SwapTx);
        }

        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseConfirming.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserDepositRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.GetAsync()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.CreateOrUpdateAsync(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);
        
        var txFlowGrain = _clusterClient.GetGrain<IOrderTxFlowGrain>(orderIndex.Id);
        await txFlowGrain.Reset(orderIndex.ToTransfer.ChainId);

        var userDepositGrain = _clusterClient.GetGrain<IUserDepositGrain>(orderIndex.Id);
        await userDepositGrain.AddOrUpdateOrder(_objectMapper.Map<OrderIndex, DepositOrderDto>(orderIndex));
        var depositOrderStatusReminderGrain =
            _clusterClient.GetGrain<IDepositOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderStatusReminderGrain)));
        await depositOrderStatusReminderGrain.AddReminder(GuidHelper.GenerateId(orderIndex.ThirdPartOrderId,
            orderIndex.Id.ToString()));

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseConfirming.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRequestRefundExceptionAsync))]
    public async Task<OrderOperationStatusDto> RequestRefundTokenAsync(GetRequestRefundDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Withdraw.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || (orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString()
                                   && orderIndex.FromTransfer.Status !=
                                   OrderTransferStatusEnum.Transferred.ToString()))
        {
            throw new UserFriendlyException("Invalid order.");
        }
        
        var amount = orderIndex.FromTransfer.Amount.ToString(await _networkAppService.GetDecimalsAsync(ChainId.AELF, 
            orderIndex.FromTransfer.Symbol), DecimalHelper.RoundingOption.Floor);
        if (orderIndex.FromTransfer.FromAddress != request.FromAddress
            || amount.SafeToDecimal() != request.Amount.SafeToDecimal()
            || orderIndex.FromTransfer.Symbol != request.Symbol
            || orderIndex.FromTransfer.ChainId != request.ChainId
            || orderIndex.FromTransfer.Network != CommonConstant.Network.AElf)
        {
            throw new UserFriendlyException("Invalid param.");
        }
        
        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus) &&
            (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.RefundRequested.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.RefundConfirming.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.RefundConfirmed.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseRequested.ToString()||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseConfirming.ToString() ||
            orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
            OrderOperationStatusEnum.ReleaseConfirmed.ToString()))
        {
            throw new UserFriendlyException("Invalid request.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.RefundRequested.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.RefundRequested.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRejectRefundExceptionAsync))]
    public async Task<OrderOperationStatusDto> RejectRefundTokenAsync(GetOrderOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.ExtensionInfo.IsNullOrEmpty()
                               || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
                               || orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                               OrderOperationStatusEnum.RefundRequested.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RefundUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RefundTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.RefundRejected.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.RefundRejected.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRefundExceptionAsync))]
    public async Task<OrderOperationStatusDto> RefundTokenAsync(GetOrderSafeOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var address = await VerifyCode(request.Code);

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Withdraw.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || (orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString()
                                   && orderIndex.FromTransfer.Status !=
                                   OrderTransferStatusEnum.Transferred.ToString()))
        {
            throw new UserFriendlyException("Invalid order.");
        }

        if (orderIndex.ExtensionInfo.IsNullOrEmpty()
            || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
            || (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.RefundRequested.ToString()
                && orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.RefundFailed.ToString()))
        {
            throw new UserFriendlyException("Invalid refund status.");
        }

        var newOrderId = Guid.NewGuid();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RefundUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RefundTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.RefundConfirming.ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RelatedOrderId,
            newOrderId.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        var userWithdrawGrain = _clusterClient.GetGrain<IUserWithdrawGrain>(newOrderId);
        await userWithdrawGrain.CreateRefundOrder(order, address);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.RefundConfirming.ToString()
        };
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRequestTransferReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> RequestTransferReleaseTokenAsync(GetRequestReleaseDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Withdraw.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        var amount = orderIndex.ToTransfer.Amount.ToString(await _networkAppService.GetDecimalsAsync(ChainId.AELF, 
            orderIndex.ToTransfer.Symbol), DecimalHelper.RoundingOption.Floor);
        if (orderIndex.ToTransfer.ToAddress != request.ToAddress
            || amount.SafeToDecimal() != request.Amount.SafeToDecimal()
            || orderIndex.ToTransfer.Symbol != request.Symbol
            || (orderIndex.ToTransfer.Network != request.ChainId &&
                orderIndex.ToTransfer.ChainId != request.ChainId))
        {
            throw new UserFriendlyException("Invalid param.");
        }

        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus) &&
            (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.RefundRequested.ToString() ||
             orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.RefundConfirming.ToString() ||
             orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.RefundConfirmed.ToString() ||
             orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.ReleaseRequested.ToString()||
             orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.ReleaseConfirming.ToString() ||
             orderIndex.ExtensionInfo[ExtensionKey.SubStatus] ==
             OrderOperationStatusEnum.ReleaseConfirmed.ToString()))
        {
            throw new UserFriendlyException("Invalid request.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.RequestTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseRequested.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);
        
        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseRequested.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleRejectTransferReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> RejectTransferReleaseTokenAsync(GetOrderOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.ExtensionInfo.IsNullOrEmpty()
                               || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
                               || orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                               OrderOperationStatusEnum.ReleaseRequested.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        orderIndex.ExtensionInfo ??= new Dictionary<string, string>();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseRejected.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseRejected.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ReconciliationAppService),
        MethodName = nameof(HandleTransferReleaseExceptionAsync))]
    public async Task<OrderOperationStatusDto> TransferReleaseTokenAsync(GetOrderSafeOperationDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new UserFriendlyException("Invalid user.");
        
        await VerifyCode(request.Code);

        var orderIndex = await GetOrderIndexAsync(request.OrderId);
        if (orderIndex == null || orderIndex.OrderType != OrderTypeEnum.Withdraw.ToString()
                               || orderIndex.Status == OrderStatusEnum.Finish.ToString()
                               || orderIndex.Status == OrderStatusEnum.ToTransferConfirmed.ToString()
                               || orderIndex.FromTransfer.Status != OrderTransferStatusEnum.Confirmed.ToString())
        {
            throw new UserFriendlyException("Invalid order.");
        }

        if (orderIndex.ExtensionInfo.IsNullOrEmpty()
            || !orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)
            || (orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.ReleaseRequested.ToString()
                && orderIndex.ExtensionInfo[ExtensionKey.SubStatus] !=
                OrderOperationStatusEnum.ReleaseFailed.ToString()))
        {
            throw new UserFriendlyException("Invalid release status.");
        }

        orderIndex.Status = OrderStatusEnum.ToStartTransfer.ToString();
        orderIndex.ToTransfer.Status = OrderTransferStatusEnum.StartTransfer.ToString();
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseUser, CurrentUser?.Name);
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.ReleaseTime,
            DateTime.UtcNow.ToUtcMilliSeconds().ToString());
        orderIndex.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus,
            OrderOperationStatusEnum.ReleaseConfirming.ToString());
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(orderIndex.Id);
        var order = (await recordGrain.Get()).Value;
        order.ExtensionInfo = orderIndex.ExtensionInfo;
        await recordGrain.AddOrUpdate(order);
        await _orderIndexRepository.AddOrUpdateAsync(orderIndex);

        if (orderIndex.ToTransfer.Network == CommonConstant.Network.AElf)
        {
            var txFlowGrain = _clusterClient.GetGrain<IOrderTxFlowGrain>(orderIndex.Id);
            await txFlowGrain.Reset(orderIndex.ToTransfer.ChainId);
        }

        var userWithdrawGrain = _clusterClient.GetGrain<IUserWithdrawGrain>(orderIndex.Id);
        await userWithdrawGrain.AddOrUpdateOrder(_objectMapper.Map<OrderIndex, WithdrawOrderDto>(orderIndex));

        return new OrderOperationStatusDto
        {
            Status = OrderOperationStatusEnum.ReleaseConfirming.ToString()
        };
    }

    [ExceptionHandler(typeof(Exception), LogLevel = Microsoft.Extensions.Logging.LogLevel.Error, 
        Message = "Save token pool error", ReturnDefault = ReturnDefault.Default)]
    public async Task<bool> AddOrUpdateTokenPoolAsync(TokenPoolDto dto)
    {
        var index = _objectMapper.Map<TokenPoolDto, TokenPoolIndex>(dto);
        await _tokenPoolIndexRepository.AddOrUpdateAsync(index);
        return true;
    }

    public async Task<PoolOverviewListDto> GetPoolOverviewAsync()
    {
        var result = new PoolOverviewListDto();

        var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        var tokenPoolDto = await tokenPoolGrain.Get();
        if (tokenPoolDto == null)
        {
            tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
            tokenPoolDto = await tokenPoolGrain.Get();
        }
        var tokenPoolInitGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var tokenPoolInitDto = await tokenPoolInitGrain.Get();
        
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var item in symbolList)
        {
            var detailDto = new PoolOverviewDetailDto
            {
                Symbol = item,
                CurrentAmount = tokenPoolDto != null && tokenPoolDto.Pool.ContainsKey(item)
                    ? tokenPoolDto.Pool[item].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                    : "0",
                InitAmount = tokenPoolInitDto != null && tokenPoolInitDto.Pool.ContainsKey(item)
                    ? tokenPoolInitDto.Pool[item].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                    : "0"
            };
            detailDto.ChangeAmount =
                (detailDto.CurrentAmount.SafeToDecimal() - detailDto.InitAmount.SafeToDecimal()).ToString();
            var exchange = 0M;
            try
            {
                exchange = detailDto.CurrentAmount.SafeToDecimal() == 0M 
                           && detailDto.InitAmount.SafeToDecimal() == 0M
                           && detailDto.ChangeAmount.SafeToDecimal() == 0M
                    ? 0M
                    : await _networkAppService.GetAvgExchangeAsync(item, CommonConstant.Symbol.USD);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Rec GetPoolOverviewAsync exchange error, {symbol}", item);
            }

            detailDto.CurrentAmountUsd =
                (detailDto.CurrentAmount.SafeToDecimal() * exchange).ToString(6, DecimalHelper.RoundingOption.Floor);
            detailDto.InitAmountUsd =
                (detailDto.InitAmount.SafeToDecimal() * exchange).ToString(6, DecimalHelper.RoundingOption.Floor);
            detailDto.ChangeAmountUsd =
                (detailDto.ChangeAmount.SafeToDecimal() * exchange).ToString(6, DecimalHelper.RoundingOption.Floor);
            result.Pool.Add(detailDto);
            result.Total.CurrentTotalAmountUsd = (result.Total.CurrentTotalAmountUsd.SafeToDecimal() +
                                                  detailDto.CurrentAmountUsd.SafeToDecimal()).ToString();
            result.Total.InitTotalAmountUsd = (result.Total.InitTotalAmountUsd.SafeToDecimal() +
                                                  detailDto.InitAmountUsd.SafeToDecimal()).ToString();
            result.Total.ChangeTotalAmountUsd = (result.Total.ChangeTotalAmountUsd.SafeToDecimal() +
                                               detailDto.ChangeAmountUsd.SafeToDecimal()).ToString();
        }

        return result;
    }

    public async Task<bool> ResetPoolInitAsync(GetPoolRequestDto request)
    {
        if (request.Symbol.IsNullOrWhiteSpace() ||
            !decimal.TryParse(request.ResetAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return false;
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        if (!symbolList.Contains(request.Symbol))
            return false;
        
        var tokenPoolInitGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var dto = await tokenPoolInitGrain.Get();
        if (dto == null) dto = new TokenPoolDto();
        dto.Pool.AddOrReplace(request.Symbol, request.ResetAmount);
        await tokenPoolInitGrain.AddOrUpdate(dto);
        return true;
    }

    public async Task<PoolChangeListDto<PoolChangeDto>> GetPoolChangeListAsync(PagedAndSortedResultRequestDto request)
    {
        var dto = new Dictionary<string, List<PoolChangeDto>>();

        var (count, list) = await GetTokenPoolIndexChangeListAsync(request, 0);
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var changeItem in list)
        {
            foreach (var item in symbolList)
            {
                if (!dto.ContainsKey(changeItem.Date)) dto.Add(changeItem.Date, new List<PoolChangeDto>());
                dto[changeItem.Date].Add(new PoolChangeDto
                {
                    Symbol = item,
                    ChangeAmount = changeItem.Pool != null && changeItem.Pool.ContainsKey(item)
                        ? changeItem.Pool[item].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                            .RemoveTrailingZeros()
                        : "0"
                });
            }
        }
        
        return new PoolChangeListDto<PoolChangeDto>
        {
            TotalCount = count,
            Pool = dto
        };
    }

    public async Task<MultiPoolOverviewDto> GetMultiPoolOverviewAsync()
    {
        var result = new MultiPoolOverviewDto();

        var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        var tokenPoolDto = await tokenPoolGrain.Get();
        if (tokenPoolDto == null)
        {
            tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
            tokenPoolDto = await tokenPoolGrain.Get();
        }
        var tokenPoolThresholdGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var tokenPoolThresholdDto = await tokenPoolThresholdGrain.Get();
        
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var item in symbolList)
        {
            var symbol = MappingItems.ContainsKey(item) ? MappingItems[item] : item;
            if (!result.MultiPool.ContainsKey("Total"))
                result.MultiPool.Add("Total", new List<PoolOverviewDto>());
            result.MultiPool["Total"].Add(new PoolOverviewDto
            {
                Symbol = item,
                CurrentAmount = tokenPoolDto != null && tokenPoolDto.MultiPool.ContainsKey(symbol)
                    ? tokenPoolDto.MultiPool[symbol]
                    : "0",
                ThresholdAmount = CalculateThresholdTotal(tokenPoolThresholdDto.MultiPool, symbol).ToString()
            });
        }

        var networkList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        foreach (var kv in networkList)
        {
            if (kv.Key == ChainId.AELF || kv.Key == ChainId.tDVV || kv.Key == ChainId.tDVW) continue;

            foreach (var key in kv.Value.Keys)
            {
                var network = MappingItems.ContainsKey(kv.Key) ? MappingItems[kv.Key] : kv.Key;
                var symbol = MappingItems.ContainsKey(key) ? MappingItems[key] : key;
                var tpKey = string.Join(CommonConstant.Underline, network, symbol);
                if (!result.MultiPool.ContainsKey(kv.Key))
                    result.MultiPool.Add(kv.Key, new List<PoolOverviewDto>());
                result.MultiPool[kv.Key].Add(new PoolOverviewDto
                {
                    Symbol = key,
                    CurrentAmount = tokenPoolDto != null && tokenPoolDto.MultiPool.ContainsKey(tpKey)
                        ? tokenPoolDto.MultiPool[tpKey]
                        : "0",
                    ThresholdAmount = tokenPoolThresholdDto != null &&
                                      tokenPoolThresholdDto.MultiPool.ContainsKey(tpKey)
                        ? tokenPoolThresholdDto.MultiPool[tpKey]
                        : "0",
                });
            }
        }

        return result;
    }

    public async Task<bool> ResetMultiPoolThresholdAsync(GetMultiPoolRequestDto request)
    {
        if (request.Network.IsNullOrWhiteSpace() || request.Symbol.IsNullOrWhiteSpace() ||
            !decimal.TryParse(request.ResetAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return false;
        var tokenList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        if (!tokenList.ContainsKey(request.Network) || !tokenList[request.Network].ContainsKey(request.Symbol))
            return false;
        if (request.Network == ChainId.AELF || request.Network == ChainId.tDVV || request.Network == ChainId.tDVW)
            return false;
        
        var tokenPoolThresholdGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var dto = await tokenPoolThresholdGrain.Get();
        if (dto == null) dto = new TokenPoolDto();
        var network = MappingItems.ContainsKey(request.Network) ? MappingItems[request.Network] : request.Network;
        var symbol = MappingItems.ContainsKey(request.Symbol) ? MappingItems[request.Symbol] : request.Symbol;
        var coin = string.Join(CommonConstant.Underline, network, symbol);
        dto.MultiPool.AddOrReplace(coin, request.ResetAmount);
        await tokenPoolThresholdGrain.AddOrUpdate(dto);
        return true;
    }
    
    public async Task<MultiPoolChangeListDto<MultiPoolChangeDto>> GetMultiPoolChangeListAsync(PagedAndSortedResultRequestDto request)
    {
        var dto = new Dictionary<string, List<MultiPoolChangeDto>>();

        var (count, list) = await GetTokenPoolIndexChangeListAsync(request);
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        var networkList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        foreach (var changeItem in list)
        {
            foreach (var item in symbolList)
            {
                var symbol = MappingItems.ContainsKey(item) ? MappingItems[item] : item;
                if(!dto.ContainsKey(changeItem.Date)) dto.Add(changeItem.Date, new List<MultiPoolChangeDto>());
                dto[changeItem.Date].Add(new MultiPoolChangeDto
                {
                    Symbol = item,
                    Network = string.Empty,
                    ChangeAmount = changeItem.MultiPool != null && changeItem.MultiPool.ContainsKey(symbol)
                        ? changeItem.MultiPool[symbol].RemoveTrailingZeros()
                        : "0",
                });
            }
            foreach (var kv in networkList)
            {
                if (kv.Key == ChainId.AELF || kv.Key == ChainId.tDVV || kv.Key == ChainId.tDVW) continue;
                
                foreach (var key in kv.Value.Keys)
                {
                    var network = MappingItems.ContainsKey(kv.Key) ? MappingItems[kv.Key] : kv.Key;
                    var symbol = MappingItems.ContainsKey(key) ? MappingItems[key] : key;
                    var tpKey = string.Join(CommonConstant.Underline, network, symbol);
                    dto[changeItem.Date].Add(new MultiPoolChangeDto
                    {
                        Symbol = key,
                        Network = kv.Key,
                        ChangeAmount = changeItem.MultiPool != null && changeItem.MultiPool.ContainsKey(tpKey)
                            ? changeItem.MultiPool[tpKey].RemoveTrailingZeros()
                            : "0"
                    });
                }
            }
        }
        
        return new MultiPoolChangeListDto<MultiPoolChangeDto>
        {
            TotalCount = count,
            MultiPool = dto
        };
    }
    
    public async Task<TokenPoolOverviewDto> GetTokenPoolOverviewAsync()
    {
        var result = new TokenPoolOverviewDto();

        var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        var tokenPoolDto = await tokenPoolGrain.Get();
        if (tokenPoolDto == null)
        {
            tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
            tokenPoolDto = await tokenPoolGrain.Get();
        }
        var tokenPoolThresholdGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var tokenPoolThresholdDto = await tokenPoolThresholdGrain.Get();
        
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var item in symbolList)
        {
            if (!result.TokenPool.ContainsKey("Total"))
                result.TokenPool.Add("Total", new List<PoolOverviewDto>());
            result.TokenPool["Total"].Add(new PoolOverviewDto
            {
                Symbol = item,
                CurrentAmount = tokenPoolDto != null && tokenPoolDto.TokenPool.ContainsKey(item)
                    ? tokenPoolDto.TokenPool[item]
                    : "0",
                ThresholdAmount = CalculateThresholdTotal(tokenPoolThresholdDto.TokenPool, item).ToString()
            });
        }

        var networkList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        foreach (var kv in networkList)
        {
            if (kv.Key != ChainId.AELF && kv.Key != ChainId.tDVV && kv.Key != ChainId.tDVW) continue;

            foreach (var key in kv.Value.Keys)
            {
                var tpKey = string.Join(CommonConstant.Underline, kv.Key, key);
                if (!result.TokenPool.ContainsKey(kv.Key))
                    result.TokenPool.Add(kv.Key, new List<PoolOverviewDto>());
                result.TokenPool[kv.Key].Add(new PoolOverviewDto
                {
                    Symbol = key,
                    CurrentAmount = tokenPoolDto != null && tokenPoolDto.TokenPool.ContainsKey(tpKey)
                        ? tokenPoolDto.TokenPool[tpKey]
                        : "0",
                    ThresholdAmount = tokenPoolThresholdDto != null &&
                                      tokenPoolThresholdDto.TokenPool.ContainsKey(tpKey)
                        ? tokenPoolThresholdDto.TokenPool[tpKey]
                        : "0",
                });
            }
        }

        return result;
    }
    
    public async Task<bool> ResetTokenPoolThresholdAsync(GetTokenPoolRequestDto request)
    {
        if (request.ChainId.IsNullOrWhiteSpace() || request.Symbol.IsNullOrWhiteSpace() ||
            !decimal.TryParse(request.ResetAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return false;
        var tokenList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        if (!tokenList.ContainsKey(request.ChainId) || !tokenList[request.ChainId].ContainsKey(request.Symbol))
            return false;
        if (request.ChainId != ChainId.AELF && request.ChainId != ChainId.tDVV && request.ChainId != ChainId.tDVW)
            return false;
        
        var tokenPoolThresholdGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var dto = await tokenPoolThresholdGrain.Get();
        if (dto == null) dto = new TokenPoolDto();
        var coin = string.Join(CommonConstant.Underline, request.ChainId, request.Symbol);
        dto.TokenPool.AddOrReplace(coin, request.ResetAmount);
        await tokenPoolThresholdGrain.AddOrUpdate(dto);
        return true;
    }
    
    public async Task<TokenPoolChangeListDto<TokenPoolChangeDto>> GetTokenPoolChangeListAsync(PagedAndSortedResultRequestDto request)
    {
        var dto = new Dictionary<string, List<TokenPoolChangeDto>>();

        var (count, list) = await GetTokenPoolIndexChangeListAsync(request);
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        var networkList = await _networkAppService.GetNetworkTokenListAsync(new GetNetworkTokenListRequestDto());
        foreach (var changeItem in list)
        {
            foreach (var item in symbolList)
            {
                if(!dto.ContainsKey(changeItem.Date)) dto.Add(changeItem.Date, new List<TokenPoolChangeDto>());
                dto[changeItem.Date].Add(new TokenPoolChangeDto
                {
                    Symbol = item,
                    ChainId = string.Empty,
                    ChangeAmount = changeItem.TokenPool != null && changeItem.TokenPool.ContainsKey(item)
                        ? changeItem.TokenPool[item].RemoveTrailingZeros()
                        : "0",
                });
            }
            foreach (var kv in networkList)
            {
                if (kv.Key != ChainId.AELF && kv.Key != ChainId.tDVV && kv.Key != ChainId.tDVW) continue;
                
                foreach (var key in kv.Value.Keys)
                {
                    var tpKey = string.Join(CommonConstant.Underline, kv.Key, key);
                    dto[changeItem.Date].Add(new TokenPoolChangeDto
                    {
                        Symbol = key,
                        ChainId = kv.Key,
                        ChangeAmount = changeItem.TokenPool != null && changeItem.TokenPool.ContainsKey(tpKey)
                            ? changeItem.TokenPool[tpKey].RemoveTrailingZeros()
                            : "0"
                    });
                }
            }
        }
        
        return new TokenPoolChangeListDto<TokenPoolChangeDto>
        {
            TotalCount = count,
            TokenPool = dto
        };
    }
    
    public async Task<Tuple<Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>>> GetFeeListAsync(bool includeAll)
    {
        var thirdPartFee = await QueryThirdPartFeeSumAggAsync(includeAll, 0);
        var withdrawFee = await QueryOrderFeeSumAggAsync(includeAll, 1);
        var depositFee = await QueryOrderFeeSumAggAsync(includeAll, 2);
        return Tuple.Create(thirdPartFee, withdrawFee, depositFee);
    }

    public async Task<FeeOverviewDto> GetFeeOverviewAsync()
    {
        var result = new FeeOverviewDto();

        var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        var tokenPoolDto = await tokenPoolGrain.Get();
        if (tokenPoolDto == null)
        {
            tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
            tokenPoolDto = await tokenPoolGrain.Get();
        }
        var tokenPoolInitGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var tokenPoolInitDto = await tokenPoolInitGrain.Get();
        
        if (tokenPoolDto != null)
        {
            foreach (var kvp in tokenPoolDto.ThirdPoolFeeInfo)
            {
                var itemDto = new FeeItemDto
                {
                    Symbol = kvp.Key,
                    CurrentAmount = tokenPoolDto.ThirdPoolFeeInfo[kvp.Key],
                    InitAmount = tokenPoolInitDto != null && tokenPoolInitDto.ThirdPoolFeeInfo.ContainsKey(kvp.Key)
                        ? tokenPoolInitDto.ThirdPoolFeeInfo[kvp.Key]
                        : "0"
                };
                var exchange = 0M;
                var symbol = kvp.Key.Split(CommonConstant.Underline).LastOrDefault();
                try
                {
                    exchange = itemDto.ChangeAmount.SafeToDecimal() == 0M
                        ? 0M
                        : await _networkAppService.GetAvgExchangeAsync(symbol, CommonConstant.Symbol.USD);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Rec GetFeeOverviewAsync exchange error, {symbol}", symbol);
                }
                itemDto.ChangeAmount =
                    (itemDto.CurrentAmount.SafeToDecimal() - itemDto.InitAmount.SafeToDecimal()).ToString();
                itemDto.ChangeAmountUsd =
                    (itemDto.ChangeAmount.SafeToDecimal() * exchange).ToString(6, DecimalHelper.RoundingOption.Floor);
                result.Fee.ThirdPart.Items.Add(itemDto);
                result.Fee.ThirdPart.TotalUsd =
                    (result.Fee.ThirdPart.TotalUsd.SafeToDecimal() + itemDto.ChangeAmountUsd.SafeToDecimal())
                    .ToString();
            }
        }
        
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var item in symbolList)
        {
            var detailDto = new FeeItemDto
            {
                Symbol = item,
                InitAmount = tokenPoolInitDto != null && tokenPoolInitDto.WithdrawFeeInfo.ContainsKey(item)
                    ? tokenPoolInitDto.WithdrawFeeInfo[item].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                    : "0"
            };
            var withdrawFee = tokenPoolDto != null && tokenPoolDto.WithdrawFeeInfo.ContainsKey(item)
                ? tokenPoolDto.WithdrawFeeInfo[item].SafeToDecimal()
                : 0;
            var depositFee = tokenPoolDto != null && tokenPoolDto.DepositFeeInfo.ContainsKey(item)
                ? tokenPoolDto.DepositFeeInfo[item].SafeToDecimal()
                : 0;
            detailDto.CurrentAmount = (withdrawFee + depositFee).ToString(6, DecimalHelper.RoundingOption.Floor);
            detailDto.ChangeAmount =
                (detailDto.CurrentAmount.SafeToDecimal() - detailDto.InitAmount.SafeToDecimal()).ToString();
            var exchange = 0M;
            try
            {
                exchange = detailDto.ChangeAmount.SafeToDecimal() == 0M
                    ? 0M
                    : await _networkAppService.GetAvgExchangeAsync(item, CommonConstant.Symbol.USD);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Rec GetFeeOverviewAsync exchange error, {symbol}", item);
            }

            detailDto.ChangeAmountUsd =
                (detailDto.ChangeAmount.SafeToDecimal() * exchange).ToString(6, DecimalHelper.RoundingOption.Floor);
            result.Fee.Etransfer.Items.Add(detailDto);
            result.Fee.Etransfer.TotalUsd =
                (result.Fee.Etransfer.TotalUsd.SafeToDecimal() + detailDto.ChangeAmountUsd.SafeToDecimal())
                .ToString();
        }

        var subsidyFee = new FeeItemDto
        {
            Symbol = TokenSymbol.ELF,
            CurrentAmount = "0",
            InitAmount = tokenPoolInitDto != null && tokenPoolInitDto.DepositFeeInfo.ContainsKey(TokenSymbol.ELF)
                ? tokenPoolInitDto.DepositFeeInfo[TokenSymbol.ELF].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                : "0"
        };
        subsidyFee.ChangeAmount =
            (subsidyFee.CurrentAmount.SafeToDecimal() - subsidyFee.InitAmount.SafeToDecimal()).ToString();
        result.Fee.Subsidy.Items.Add(subsidyFee);

        return result;
    }

    public async Task<bool> ResetFeeInitAsync(GetFeeRequestDto request)
    {
        if (request.Symbol.IsNullOrWhiteSpace() ||
            !decimal.TryParse(request.ResetAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return false;
        
        var tokenPoolInitGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
            DateTime.MinValue.Date.ToUtcString(TimeHelper.DatePattern));
        var dto = await tokenPoolInitGrain.Get();
        if (dto == null) dto = new TokenPoolDto();

        if (request.Type == 0)
        {
            var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
            var tokenPoolDto = await tokenPoolGrain.Get();
            if (tokenPoolDto == null)
            {
                tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                    ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
                tokenPoolDto = await tokenPoolGrain.Get();
            }

            if (tokenPoolDto != null)
            {
                if (tokenPoolDto.ThirdPoolFeeInfo.ContainsKey(request.Symbol))
                {
                    dto.ThirdPoolFeeInfo.AddOrReplace(request.Symbol, request.ResetAmount);
                    await tokenPoolInitGrain.AddOrUpdate(dto);
                    return true;
                }
            }

            return false;
        }
        else if (request.Type == 1){
            var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
            if (!symbolList.Contains(request.Symbol))
                return false;
            dto.WithdrawFeeInfo.AddOrReplace(request.Symbol, request.ResetAmount);
            await tokenPoolInitGrain.AddOrUpdate(dto);
            return true;
        }

        if (request.Symbol != TokenSymbol.ELF) return false;
        dto.DepositFeeInfo.AddOrReplace(request.Symbol, request.ResetAmount);
        await tokenPoolInitGrain.AddOrUpdate(dto);
        return true;
    }

    public async Task<FeeChangeListDto<FeeChangeDto>> GetFeeChangeListAsync(PagedAndSortedResultRequestDto request)
    {
        var dto = new Dictionary<string, Dictionary<string, List<FeeChangeDto>>>();

        var (count, list) = await GetTokenPoolIndexChangeListAsync(request, 3);
        var tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        var tokenPoolDto = await tokenPoolGrain.Get();
        if (tokenPoolDto == null)
        {
            tokenPoolGrain = _clusterClient.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
            tokenPoolDto = await tokenPoolGrain.Get();
        }
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var changeItem in list)
        {
            if (!dto.ContainsKey(changeItem.Date)) dto.Add(changeItem.Date, new Dictionary<string, List<FeeChangeDto>>());
            if (tokenPoolDto != null)
            {
                foreach (var kvp in tokenPoolDto.ThirdPoolFeeInfo)
                {
                    if (!dto[changeItem.Date].ContainsKey("ThirdPart"))
                        dto[changeItem.Date].Add("ThirdPart", new List<FeeChangeDto>());
                    dto[changeItem.Date]["ThirdPart"].Add(new FeeChangeDto
                    {
                        Symbol = kvp.Key,
                        ChangeAmount = changeItem.ThirdPoolFeeInfo != null && changeItem.ThirdPoolFeeInfo.ContainsKey(kvp.Key)
                            ? changeItem.ThirdPoolFeeInfo[kvp.Key].SafeToDecimal().ToString(6, DecimalHelper.RoundingOption.Floor)
                                .RemoveTrailingZeros()
                            : "0"
                    });
                }
            }

            foreach (var item in symbolList)
            {
                if (!dto[changeItem.Date].ContainsKey("Etransfer"))
                    dto[changeItem.Date].Add("Etransfer", new List<FeeChangeDto>());
                var changeDto = new FeeChangeDto
                {
                    Symbol = item
                };
                var withdrawChangeFee =
                    changeItem.WithdrawFeeInfo != null && changeItem.WithdrawFeeInfo.ContainsKey(item)
                        ? changeItem.WithdrawFeeInfo[item].SafeToDecimal()
                        : 0;
                var depositChangeFee = changeItem.DepositFeeInfo != null && changeItem.DepositFeeInfo.ContainsKey(item)
                    ? changeItem.DepositFeeInfo[item].SafeToDecimal()
                    : 0;
                changeDto.ChangeAmount = (withdrawChangeFee + depositChangeFee)
                    .ToString(6, DecimalHelper.RoundingOption.Floor)
                    .RemoveTrailingZeros();
                dto[changeItem.Date]["Etransfer"].Add(changeDto);
            }
            
            if (!dto[changeItem.Date].ContainsKey("Subsidy"))
                dto[changeItem.Date].Add("Subsidy", new List<FeeChangeDto>());
            dto[changeItem.Date]["Subsidy"].Add(new FeeChangeDto
            {
                Symbol = TokenSymbol.ELF,
                ChangeAmount = "0"
            });
        }
        
        return new FeeChangeListDto<FeeChangeDto>
        {
            TotalCount = count,
            Fee = dto
        };
    }

    private async Task<string> VerifyCode(string code)
    {
        var userGrain = _clusterClient.GetGrain<IUserReconciliationGrain>(GuidHelper.UniqGuid(CurrentUser?.Name));
        var user = await userGrain.GetUser();
        if (!user.Success)
        {
            throw new UserFriendlyException("Invalid manage user.");
        }

        var secretHash = EncryptionHelper.Decrypt(user.Data.PasswordHash,
            _stringEncryptionOptions.Value.DefaultPassPhrase);
        var secret = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(secretHash));
        var otpCode = TotpHelper.GetCode(secret);
        if (code != otpCode)
        {
            throw new UserFriendlyException("Invalid code.");
        }

        return user.Data.Address;
    }

    private async Task<OrderIndex> GetOrderIndexAsync(string orderId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Id).Value(orderId)));

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _orderIndexRepository.GetAsync(Filter);
    }

    private async Task<Dictionary<string, string>> QuerySumAggAsync(GetOrderRequestDto request, string type)
    {
        var result = new Dictionary<string, string>();
        var mustQuery = await GetMustQueryAsync(request, type);
        if (mustQuery == null) return result;

        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(f => f.Bool(b => b.Must(mustQuery)));
        s.Aggregations(agg => agg
            .Terms("symbol", ts => ts
                .Field(f => f.FromTransfer.Symbol)
                .Aggregations(sumAgg => sumAgg
                    .Sum("sum_amount", sum => sum
                        .Field(f => f.FromTransfer.Amount))
                )
            )
        );

        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
        {
            _logger.LogError("Rec QuerySumAggAsync error: {error}", searchResponse.ServerError?.Error);
            return result;
        }

        var symbolAgg = searchResponse.Aggregations.Terms("symbol");
        foreach (var symbolBucket in symbolAgg.Buckets)
        {
            var amount = (decimal)symbolBucket.Sum("sum_amount")?.Value;
            result.Add(symbolBucket.Key, amount.ToString(4, DecimalHelper.RoundingOption.Floor));
        }

        if (type == OrderTypeEnum.Deposit.ToString())
            result.Add("fee", "0");

        return result;
    }

    private async Task<Tuple<long, List<OrderIndex>>> GetOrderRecordListAsync(GetOrderRequestDto request, string type)
    {
        var mustQuery = await GetMustQueryAsync(request, type);
        if (mustQuery == null) return Tuple.Create(0L, new List<OrderIndex>());
        var mustNotQuery = await GetMustNotQueryAsync();

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) =>
            type == OrderStatusResponseEnum.Failed.ToString()
                ? f.Bool(b => b.Must(mustQuery).MustNot(mustNotQuery))
                : f.Bool(b => b.Must(mustQuery));
        
        var(count, list) = await _orderIndexRepository.GetSortListAsync(Filter,
            sortFunc: string.IsNullOrWhiteSpace(request.Sorting)
                ? s => s.Descending(t => t.CreateTime)
                : GetSorting(request.Sorting),
            limit: request.MaxResultCount == 0 ? OrderOptions.DefaultResultCount :
            request.MaxResultCount > OrderOptions.MaxResultCount ? OrderOptions.MaxResultCount :
            request.MaxResultCount,
            skip: request.SkipCount);
        if (count == OrderOptions.DefaultMaxSize)
        {
            count = (await _orderIndexRepository.CountAsync(Filter)).Count;
        }

        return Tuple.Create(count, list);
    }

    private async Task<List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>> GetMustQueryAsync(
        GetOrderRequestDto request, string type)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        if (type == OrderStatusResponseEnum.Failed.ToString())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.FromTransfer.Status).Value(OrderTransferStatusEnum.Confirmed.ToString())));
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.FromTransfer.Amount).GreaterThan(0)));
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Terms(w =>
                    w.Field(f => f.Status).Terms(OrderStatusHelper.GetFailedList())),
                p => p.Bool(j => j.Must(
                    s => s.Range(k =>
                        k.Field(f => f.CreateTime).LessThan(DateTime.UtcNow
                            .AddDays(OrderOptions.ValidOrderMessageThreshold).ToUtcMilliSeconds())),
                    s => s.Terms(i =>
                        i.Field(f => f.Status).Terms(OrderStatusHelper.GetProcessingList())))))));
        }
        else
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.OrderType).Value(type)));
        }

        if (!request.Address.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Term(w =>
                    w.Field(f => f.FromTransfer.FromAddress).Value(request.Address.Trim()).CaseInsensitive()),
                s => s.Term(w =>
                    w.Field(f => f.FromTransfer.ToAddress).Value(request.Address.Trim()).CaseInsensitive()),
                s => s.Term(w =>
                    w.Field(f => f.ToTransfer.FromAddress).Value(request.Address.Trim()).CaseInsensitive()),
                s => s.Term(w =>
                    w.Field(f => f.ToTransfer.ToAddress).Value(request.Address.Trim()).CaseInsensitive()),
                s => s.Match(w =>
                    w.Field("extensionInfo.SwapToAddress").Query(request.Address.Trim())),
                s => s.Term(w =>
                    w.Field(f => f.FromTransfer.TxId).Value(request.Address.Trim()).CaseInsensitive()),
                s => s.Term(w =>
                    w.Field(f => f.ToTransfer.TxId).Value(request.Address.Trim()).CaseInsensitive()))));
        }

        if (request.Token > 0 || request.FromChainId > 0 || request.ToChainId > 0)
        {
            var options = await GetNetworkOptionAsync();
            if (request.Token > 0)
            {
                var token = options.TokenList.FirstOrDefault(t => t.Key == request.Token)?.Symbol;
                if (token.IsNullOrEmpty()) return null;
                mustQuery.Add(q => q.Bool(i => i.Should(
                    s => s.Term(w =>
                        w.Field(f => f.FromTransfer.Symbol).Value(token)),
                    s => s.Term(w =>
                        w.Field(f => f.ToTransfer.Symbol).Value(token)))));
            }

            if (request.FromChainId > 0)
            {
                var fromChainId = options.NetworkList.FirstOrDefault(t => t.Key == request.FromChainId)?.Network;
                if (fromChainId.IsNullOrEmpty()) return null;
                if (fromChainId == ChainId.AELF || fromChainId == ChainId.tDVV || fromChainId == ChainId.tDVW)
                {
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.FromTransfer.ChainId).Value(fromChainId)));
                }
                else
                {
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.FromTransfer.Network).Value(fromChainId)));
                }
            }

            if (request.ToChainId > 0)
            {
                var toChainId = options.NetworkList.FirstOrDefault(t => t.Key == request.ToChainId)?.Network;
                if (toChainId.IsNullOrEmpty()) return null;
                if (toChainId == ChainId.AELF || toChainId == ChainId.tDVV || toChainId == ChainId.tDVW)
                {
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.ToTransfer.ChainId).Value(toChainId)));
                }
                else
                {
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.ToTransfer.Network).Value(toChainId)));
                }
            }
        }

        if (request.StartCreateTime.HasValue)
        {
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime).GreaterThanOrEquals(request.StartCreateTime.Value)));
        }

        if (request.EndCreateTime.HasValue)
        {
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime).LessThanOrEquals(request.EndCreateTime.Value)));
        }

        return mustQuery;
    }

    private async Task<List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>> GetMustNotQueryAsync()
    {
        var mustNotQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustNotQuery.Add(q => q.Match(i =>
            i.Field("extensionInfo.RefundTx").Query(ExtensionKey.RefundTx)));
        return mustNotQuery;
    }
    
    private async Task<Dictionary<string, string>> QueryThirdPartFeeSumAggAsync(bool includeAll, int type)
    {
        var result = new Dictionary<string, string>();
        var mustQuery = await GetMustQueryFeeAsync(includeAll, type);
        if (mustQuery == null) return result;

        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(f => f.Bool(b => b.Must(mustQuery)));
        s.Aggregations(agg => agg
            .Terms("symbol", t => t
                .Field("thirdPartFee.symbol.keyword")
                .Aggregations(sumAgg => sumAgg
                    .Sum("sum_amount", sum => sum
                        .Script(script => script
                            .Source(@"
                                if (params._source.thirdPartFee != null && 
                                    params._source.thirdPartFee.length > 0) {
                                    def fee = params._source.thirdPartFee[0];
                                    def amount = Double.parseDouble(fee.amount);
                                    def decimals = Integer.parseInt(fee.decimals);
                                    return amount / Math.pow(10, decimals);
                                }
                                return 0;
                            ")
                        )
                    )
                )
            )
        );

        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
        {
            _logger.LogError("Rec QueryThirdPartFeeSumAggAsync error: {error}", searchResponse.ServerError?.Error);
            return result;
        }
        var symbolAgg = searchResponse.Aggregations.Terms("symbol");
        foreach (var symbolBucket in symbolAgg.Buckets)
        {
            var feeCoin = symbolBucket.Key.Split(CommonConstant.Underline);
            var feeSymbol = feeCoin.Length == 1 ? feeCoin[0] : feeCoin[1];
            if (!result.ContainsKey(feeSymbol))
                result.Add(feeSymbol, symbolBucket.Sum("sum_amount")?.Value.ToString().SafeToDecimal().ToString());
            else
                result[feeSymbol] = (result[feeSymbol].SafeToDouble() + symbolBucket.Sum("sum_amount")?.Value)
                    .ToString().SafeToDecimal().ToString();
        }

        return result;
    }
    
    private async Task<Dictionary<string, string>> QueryOrderFeeSumAggAsync(bool includeAll, int type)
    {
        var result = new Dictionary<string, string>();
        var mustQuery = await GetMustQueryFeeAsync(includeAll, type);
        if (mustQuery == null) return result;

        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(f => f.Bool(b => b.Must(mustQuery)));
        s.Aggregations(agg => agg
            .Terms("symbol", t => t
                .Field("toTransfer.feeInfo.symbol")
                .Aggregations(sumAgg => sumAgg
                    .Sum("sum_amount", sum => sum
                        .Script(script => script
                            .Source(@"
                                if (params._source.toTransfer.feeInfo != null && 
                                    params._source.toTransfer.feeInfo.length > 0) {
                                    def fee = params._source.toTransfer.feeInfo[0];
                                    return Double.parseDouble(fee.amount);
                                }
                                return 0;
                            ")
                        )
                    )
                )
            )
        );

        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
        {
            _logger.LogError("Rec QueryOrderFeeSumAggAsync error: {error}", searchResponse.ServerError?.Error);
            return result;
        }

        var symbolAgg = searchResponse.Aggregations.Terms("symbol");
        foreach (var symbolBucket in symbolAgg.Buckets)
        {
            var amount = (decimal)symbolBucket.Sum("sum_amount")?.Value;
            result.Add(symbolBucket.Key, amount.ToString().SafeToDecimal().ToString());
        }

        return result;
    }
    
    private async Task<List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>> GetMustQueryFeeAsync(
        bool includeAll, int type)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Status).Terms(OrderStatusHelper.GetSucceedList())));
        if (type == 2)
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.OrderType).Value(OrderTypeEnum.Deposit.ToString())));
        }
        else
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.OrderType).Value(OrderTypeEnum.Withdraw.ToString())));
        }

        if (type == 0)
        {
            mustQuery.Add(q => q.Exists(i =>
                i.Field("thirdPartFee.decimals")));
        }
        else
        {
            mustQuery.Add(q => q.Exists(i =>
                i.Field("toTransfer.feeInfo.symbol")));
        }

        if (!includeAll)
        {
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime).GreaterThanOrEquals(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds())));
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime).LessThanOrEquals(DateTime.UtcNow.Date.ToUtcMilliSeconds())));
        }

        return mustQuery;
    }

    private async Task<List<OrderRecordDto>> LoopCollectionItemsAsync(List<OrderRecordDto> itemList,
        List<OrderIndex> orderList = null, string type = null)
    {
        var fromSymbolList = itemList.Select(i => i.FromTransfer.Symbol).Distinct().ToList();
        var toSymbolList = itemList.Select(i => i.ToTransfer.Symbol).Distinct().ToList();
        fromSymbolList.AddRange(toSymbolList);
        fromSymbolList = fromSymbolList.Distinct().ToList();
        var exchangeDic = new Dictionary<string, decimal>();
        foreach (var item in fromSymbolList)
        {
            try
            {
                exchangeDic.Add(item, await _networkAppService.GetAvgExchangeAsync(item, CommonConstant.Symbol.USD));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Rec LoopCollectionItemsAsync exchange error, {symbol}", item);
                exchangeDic.Add(item, 0M);
            }
        }

        foreach (var item in itemList)
        {
            await HandleItemAsync(item);
            item.FromTransfer.Amount =
                decimal.Parse(item.FromTransfer.Amount).ToString(4, DecimalHelper.RoundingOption.Floor);
            item.FromTransfer.AmountUsd =
                (decimal.Parse(item.FromTransfer.Amount) * exchangeDic[item.FromTransfer.Symbol]).ToString(2,
                    DecimalHelper.RoundingOption.Floor);
            item.ToTransfer.Amount =
                decimal.Parse(item.ToTransfer.Amount).ToString(4, DecimalHelper.RoundingOption.Floor);
            item.ToTransfer.AmountUsd =
                (decimal.Parse(item.ToTransfer.Amount) * exchangeDic[item.ToTransfer.Symbol]).ToString(2,
                    DecimalHelper.RoundingOption.Floor);
            item.FromTransfer.Icon =
                await _networkAppService.GetIconAsync(item.OrderType, ChainId.AELF, item.FromTransfer.Symbol);
            item.ToTransfer.Icon =
                await _networkAppService.GetIconAsync(item.OrderType, ChainId.AELF, item.FromTransfer.Symbol,
                    item.ToTransfer.Symbol);
            var orderIndex = orderList.FirstOrDefault(i => i.Id == item.Id);
            if (orderIndex != null && !orderIndex.ExtensionInfo.IsNullOrEmpty() &&
                orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.SwapToMain) &&
                orderIndex.ExtensionInfo[ExtensionKey.SwapToMain].Equals(Boolean.TrueString))
            {
                item.ToTransfer.FromAddress = orderIndex.FromTransfer.Symbol == orderIndex.ToTransfer.Symbol
                    ? orderIndex.ExtensionInfo[ExtensionKey.SwapOriginFromAddress]
                    : orderIndex.ExtensionInfo[ExtensionKey.SwapFromAddress];
                item.ToTransfer.ToAddress = orderIndex.ExtensionInfo[ExtensionKey.SwapToAddress];
                item.ToTransfer.ChainId = orderIndex.ExtensionInfo[ExtensionKey.SwapChainId];
            }
            item.SecondOrderType = orderIndex != null && !orderIndex.ExtensionInfo.IsNullOrEmpty() &&
                                   orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.OrderType)
                ? orderIndex.ExtensionInfo[ExtensionKey.OrderType]
                : string.Empty;
            if (!type.IsNullOrEmpty())
            {
                var extensionInfo = orderIndex?.ExtensionInfo;
                if (!extensionInfo.IsNullOrEmpty() && extensionInfo.ContainsKey(ExtensionKey.SubStatus))
                {
                    item.OperationStatus = extensionInfo[ExtensionKey.SubStatus];
                }
                item.RoleType = await GetRoleTypeAsync();
                item.Applicant = !extensionInfo.IsNullOrEmpty() && extensionInfo.ContainsKey(ExtensionKey.RequestUser)
                    ? extensionInfo[ExtensionKey.RequestUser]
                    : null;
            }
        }

        return itemList;
    }
    
    private async Task<List<OrderMoreDetailDto>> LoopWithdrawItemsAsync(List<OrderRecordDto> itemList,
        List<OrderIndex> orderList = null)
    {
        var withdrawList = _objectMapper.Map<List<OrderRecordDto>, List<OrderMoreDetailDto>>(itemList);
        foreach (var item in withdrawList)
        {
            var itemIndex = orderList.FirstOrDefault(i => i.Id == item.Id);
            if (itemIndex == null) continue;
            item.ThirdPartFee = !itemIndex.ThirdPartFee.IsNullOrEmpty() &&
                                itemIndex.ThirdPartFee[0].Decimals.SafeToInt() > 0
                ? new FeeInfo
                {
                    Symbol = itemIndex.ThirdPartFee[0].Symbol.Split(CommonConstant.Underline).LastOrDefault(),
                    Amount = (itemIndex.ThirdPartFee[0].Amount.SafeToDecimal() /
                              (decimal)Math.Pow(10, itemIndex.ThirdPartFee[0].Decimals.SafeToInt())).ToString()
                }
                : new FeeInfo();
        }

        return withdrawList;
    }

    private async Task<int> GetRoleTypeAsync()
    {
        var roles = CurrentUser?.Roles;
        if (!roles.IsNullOrEmpty())
        {
            var role = char.ToUpper(roles[0][0]) + roles[0].Substring(1);
            if (Enum.TryParse(role, out RoleEnum result))
            {
                return (int)result;
            }
        }

        return -1;
    }

    private async Task HandleItemAsync(OrderRecordDto item)
    {
        var status = Enum.Parse<OrderStatusEnum>(item.Status);
        switch (status)
        {
            case OrderStatusEnum.ToTransferConfirmed:
            case OrderStatusEnum.Finish:
                item.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ToTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ArrivalTime = item.LastModifyTime;
                break;
            case OrderStatusEnum.FromTransferFailed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Failed.ToString();
                item.ToTransfer.Status = string.Empty;
                item.ArrivalTime = 0;
                break;
            case OrderStatusEnum.ToTransferFailed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ToTransfer.Status = OrderStatusResponseEnum.Failed.ToString();
                item.ArrivalTime = 0;
                break;
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Failed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status, status.ToString());
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status, status.ToString());
                item.ArrivalTime = 0;
                break;
            default:
                item.Status = OrderStatusResponseEnum.Processing.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status);
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status);
                break;
        }
    }
    
    private string GetTransferStatus(string transferStatus, string orderStatus = null)
    {
        if(transferStatus == CommonConstant.SuccessStatus) return OrderStatusResponseEnum.Succeed.ToString();
        try
        {
            var status = Enum.Parse<OrderTransferStatusEnum>(transferStatus);
            switch (status)
            {
                case OrderTransferStatusEnum.Confirmed:
                    return OrderStatusResponseEnum.Succeed.ToString();
                case OrderTransferStatusEnum.TransferFailed:
                case OrderTransferStatusEnum.Failed:
                    return OrderStatusResponseEnum.Failed.ToString();
                default:
                    if (orderStatus == OrderStatusEnum.Expired.ToString() 
                        || orderStatus == OrderStatusEnum.Failed.ToString())
                        return OrderStatusResponseEnum.Failed.ToString();
                    return OrderStatusResponseEnum.Processing.ToString();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Rec OrderTransferStatusEnum parse error, status={status}", transferStatus);
            if (orderStatus == OrderStatusEnum.Expired.ToString() || orderStatus == OrderStatusEnum.Failed.ToString())
                return OrderStatusResponseEnum.Failed.ToString();
            if (transferStatus.IsNullOrEmpty()) return string.Empty;
            return OrderStatusResponseEnum.Processing.ToString();
        }
    }

    private static Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>> GetSorting(string sorting)
    {
        var result =
            new Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>>(s =>
                s.Descending(t => t.CreateTime));

        var sortingArray = sorting.Trim().ToLower().Split(CommonConstant.Space, StringSplitOptions.RemoveEmptyEntries);
        switch (sortingArray.Length)
        {
            case 1:
                switch (sortingArray[0])
                {
                    case OrderOptions.CreateTime:
                        result = s =>
                            s.Ascending(t => t.CreateTime);
                        break;
                }
                break;
            case 2:
                switch (sortingArray[0])
                {
                    case OrderOptions.CreateTime:
                        result = s =>
                            sortingArray[1] == OrderOptions.Asc || sortingArray[1] == OrderOptions.Ascend
                                ? s.Ascending(t => t.CreateTime)
                                : s.Descending(t => t.CreateTime);
                        break;
                }
                break;
        }

        return result;
    }
    
    private decimal CalculateThresholdTotal(Dictionary<string, string> dic, string symbol)
    {
        var total = 0M;
        if (dic == null) return total;
        foreach (var kvp in dic)
        {
            if (kvp.Key.EndsWith(symbol))
            {
                if (decimal.TryParse(kvp.Value, out var value))
                {
                    total += value;
                }
            }
        }
        return total;
    }
    
    private async Task<Tuple<long, List<TokenPoolIndex>>> GetTokenPoolIndexChangeListAsync(PagedAndSortedResultRequestDto request, int type = -1)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TokenPoolIndex>, QueryContainer>>();
        if (type == 0)
        {
            mustQuery.Add(q => q.Exists(k =>
                k.Field("pool.USDT")));
        }
        else if(type == 3)
        {
            mustQuery.Add(q => q.Exists(k =>
                k.Field("withdrawFeeInfo.USDT")));
        }

        QueryContainer Filter(QueryContainerDescriptor<TokenPoolIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var(count, list) = await _tokenPoolIndexRepository.GetSortListAsync(Filter,
            sortFunc: s => s.Descending(t => t.Date),
            limit: request.MaxResultCount == 0 ? OrderOptions.DefaultResultCount :
            request.MaxResultCount > OrderOptions.MaxResultCount ? OrderOptions.MaxResultCount : request.MaxResultCount,
            skip: request.SkipCount);
        if (count == OrderOptions.DefaultMaxSize)
        {
            count = (await _tokenPoolIndexRepository.CountAsync(Filter)).Count;
        }

        return Tuple.Create(count, list);
    }
}