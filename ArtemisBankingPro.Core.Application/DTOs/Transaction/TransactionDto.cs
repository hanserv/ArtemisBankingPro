using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class TransactionDto : BaseDto<int>
    {
        public required decimal Amount { get; set; }
        public required TransactionType TransactionType { get; set; }
        public required string Origin { get; set; }
        public required string Beneficiary { get; set; }
        public required TransactionStatus Status { get; set; }
        public required DateTime Date { get; set; }
    }
}
