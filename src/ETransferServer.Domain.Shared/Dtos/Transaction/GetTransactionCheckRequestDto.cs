using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Transaction;

public class GetTransactionCheckRequestDto
{
    [Required]
    public string Address { get; set; }
}