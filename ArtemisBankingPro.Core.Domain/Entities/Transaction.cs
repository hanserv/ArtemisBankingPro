using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class Transaction : BaseEntity<int>
    {
        public required int SavingsAccountId { get; set; }
        public SavingsAccount? SavingsAccount { get; set; }

        public required decimal Amount { get; set; }
        public required TransactionType Type { get; set; }

        public required string Origin { get; set; }
        public required string Beneficiary { get; set; } 
        public required TransactionStatus Status { get; set; }

        public string? PerformedByUserId { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
