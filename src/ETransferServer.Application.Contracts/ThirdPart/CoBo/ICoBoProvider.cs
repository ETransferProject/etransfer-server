using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.ThirdPart.CoBo.Dtos;

namespace ETransferServer.ThirdPart.CoBo;

public interface ICoBoProvider
{
    Task<CoBoCoinDetailDto> GetCoinDetailAsync(string coin);
    Task<AccountDetailDto> GetAccountDetailAsync();
    Task<List<CoBoTransactionDto>> GetTransactionsByTimeExAsync(TransactionRequestDto input);
    Task<CoBoTransactionDto> GetTransactionAsync(string id);
    Task<AddressesDto> GetAddressesAsync(string coin, int count, bool nativeSegwit = false);
    Task<string> WithdrawAsync(WithdrawRequestDto input);
    Task<WithdrawInfoDto> GetWithdrawInfoByRequestIdAsync(string requestId);
}