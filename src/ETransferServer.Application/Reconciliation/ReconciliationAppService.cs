using System;
using System.Collections.Generic;
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
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Order;
using ETransferServer.Orders;
using ETransferServer.Service.Info;
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
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;
    private readonly IInfoAppService _infoService;
    private readonly IOrderAppService _orderService;
    private readonly INetworkAppService _networkAppService;
    private readonly IdentityUserManager _userManager;
    private readonly IOptionsSnapshot<StringEncryptionOptions> _stringEncryptionOptions;

    public ReconciliationAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger,
        IInfoAppService infoService,
        IOrderAppService orderService,
        INetworkAppService networkAppService,
        IdentityUserManager userManager,
        IOptionsSnapshot<StringEncryptionOptions> stringEncryptionOptions)
    {
        _orderIndexRepository = orderIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _infoService = infoService;
        _orderService = orderService;
        _networkAppService = networkAppService;
        _userManager = userManager;
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

        var amount = orderIndex.FromTransfer.Amount.ToString(await _networkAppService.GetDecimalsAsync(ChainId.AELF, 
            orderIndex.FromTransfer.Symbol), DecimalHelper.RoundingOption.Floor);
        if (orderIndex.ToTransfer.ToAddress != request.ToAddress
            || amount.SafeToDecimal() != request.Amount.SafeToDecimal()
            || orderIndex.FromTransfer.Symbol != request.Symbol
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
            || orderIndex.ToTransfer.Network != CommonConstant.Network.AElf)
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
}