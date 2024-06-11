using ETransferServer.Dtos.GraphQL;

namespace ETransferServer.Dtos.Order;

public class WithdrawOrderMonitorDto : TransferRecordDto
{
    public string Reason { get; set; }
    
    public static WithdrawOrderMonitorDto Create(TransferRecordDto recordDto, string reason)
    {
        return new WithdrawOrderMonitorDto
        {
            Id = recordDto.Id,
            TransactionId = recordDto.TransactionId,
            MethodName = recordDto.MethodName,
            From = recordDto.From,
            To = recordDto.To,
            ToChainId = recordDto.ToChainId,
            ToAddress = recordDto.ToAddress,
            Symbol = recordDto.Symbol,
            Amount = recordDto.Amount,
            MaxEstimateFee = recordDto.MaxEstimateFee,
            Timestamp = recordDto.Timestamp,
            TransferType = recordDto.TransferType,
            ChainId = recordDto.ChainId,
            BlockHash = recordDto.BlockHash,
            BlockHeight = recordDto.BlockHeight,
            Reason = reason
        };
    }
}