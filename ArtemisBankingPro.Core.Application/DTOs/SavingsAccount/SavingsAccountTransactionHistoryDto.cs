using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.SavingsAccount
{
    public class SavingsAccountTransactionHistoryDto
    {
        public required string AccountNumber { get; set; }
        public required string ClientFullName { get; set; }
        public required decimal Balance { get; set; }
        public required SavingsAccountType Type { get; set; }
        public required SavingsAccountStatus Status { get; set; }
        public required PagedResult<TransactionDto> Transactions { get; set; }
    }
}
