using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Token;
using ETransferServer.Models;
using ETransferServer.Network.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Network;

public interface INetworkAppService : IApplicationService
{
    Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request);
    Task<GetNetworkListDto> GetNetworkListWithLocalFeeAsync(GetNetworkListRequestDto request);
    Task<Tuple<decimal, CoBoCoinDto>> CalculateNetworkFeeAsync(string network, string symbol);
    Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol, long timestamp = 0L);
    Task<decimal> GetMinThirdPartFeeAsync(string symbol);
    Task<int> GetDecimalsAsync(string chainId, string symbol);
}