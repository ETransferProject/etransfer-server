using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Dtos.Token;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Reconciliation;

public interface IReconciliationAppService : IApplicationService
{
    Task<GetTokenOptionResultDto> GetNetworkOptionAsync();
    Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request);
    Task<bool> InitUserAsync(GetUserDto request);
    Task<OrderMoreDetailDto> GetOrderRecordDetailAsync(string id);
    Task<OrderPagedResultDto<OrderRecordDto>> GetDepositOrderRecordListAsync(GetOrderRequestDto request);
    Task<OrderPagedResultDto<OrderMoreDetailDto>> GetWithdrawOrderRecordListAsync(GetOrderRequestDto request);
    Task<PagedResultDto<OrderRecordDto>> GetFailOrderRecordListAsync(GetOrderRequestDto request);
    Task<OrderOperationStatusDto> RequestReleaseTokenAsync(GetRequestReleaseDto request);
    Task<OrderOperationStatusDto> RejectReleaseTokenAsync(GetOrderOperationDto request);
    Task<OrderOperationStatusDto> ReleaseTokenAsync(GetOrderSafeOperationDto request);
    Task<OrderOperationStatusDto> RequestRefundTokenAsync(GetRequestRefundDto request);
    Task<OrderOperationStatusDto> RejectRefundTokenAsync(GetOrderOperationDto request);
    Task<OrderOperationStatusDto> RefundTokenAsync(GetOrderSafeOperationDto request);
    Task<OrderOperationStatusDto> RequestTransferReleaseTokenAsync(GetRequestReleaseDto request);
    Task<OrderOperationStatusDto> RejectTransferReleaseTokenAsync(GetOrderOperationDto request);
    Task<OrderOperationStatusDto> TransferReleaseTokenAsync(GetOrderSafeOperationDto request);
    Task<bool> AddOrUpdateTokenPoolAsync(TokenPoolDto dto);
    Task<PoolOverviewListDto> GetPoolOverviewAsync();
    Task<bool> ResetPoolInitAsync(GetPoolRequestDto request);
    Task<PoolChangeListDto<PoolChangeDto>> GetPoolChangeListAsync(PagedAndSortedResultRequestDto request);
    Task<MultiPoolOverviewDto> GetMultiPoolOverviewAsync();
    Task<bool> ResetMultiPoolThresholdAsync(GetMultiPoolRequestDto request);
    Task<MultiPoolChangeListDto<MultiPoolChangeDto>> GetMultiPoolChangeListAsync(PagedAndSortedResultRequestDto request);
    Task<TokenPoolOverviewDto> GetTokenPoolOverviewAsync();
    Task<bool> ResetTokenPoolThresholdAsync(GetTokenPoolRequestDto request);
    Task<TokenPoolChangeListDto<TokenPoolChangeDto>> GetTokenPoolChangeListAsync(PagedAndSortedResultRequestDto request);
    Task<Tuple<Dictionary<string, string>, Dictionary<string, string>, Dictionary<string, string>>> GetFeeListAsync(bool includeAll);
    Task<FeeOverviewDto> GetFeeOverviewAsync();
    Task<bool> ResetFeeInitAsync(GetFeeRequestDto request);
    Task<FeeChangeListDto<FeeChangeDto>> GetFeeChangeListAsync(PagedAndSortedResultRequestDto request);
}