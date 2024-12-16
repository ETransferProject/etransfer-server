using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Token;
using ETransferServer.Models;
using ETransferServer.Network.Dtos;
using ETransferServer.Token.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Network;

public interface INetworkAppService : IApplicationService
{
    Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request, string version = null);
    Task<GetNetworkListDto> GetNetworkListWithLocalFeeAsync(GetNetworkListRequestDto request, string version = null, bool isAddressSupport = false);
    Task<Tuple<decimal, CoBoCoinDto>> CalculateNetworkFeeAsync(string network, string symbol);
    Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol, long timestamp = 0L);
    Task<decimal> GetMinThirdPartFeeAsync(string network, string symbol);
    Task<decimal> GetMaxThirdPartFeeAsync(string network, string symbol);
    Task<int> GetDecimalsAsync(string chainId, string symbol);
    Task<string> GetIconAsync(string orderType, string chainId, string fromSymbol, string toSymbol = null);
    Task<ListResultDto<TokenPriceDataDto>> GetTokenPriceListAsync(GetTokenPriceListRequestDto request);
}