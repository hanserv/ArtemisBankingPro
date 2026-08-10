using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class SavingsAccount : BaseEntity<int>
    {
        public required string AccountNumber { get; set; }
        public required string ClientId { get; set; }
        public required decimal Balance { get; set; }
        public required SavingsAccountType Type { get; set; }
        public required SavingsAccountStatus Status { get; set; } 
        public required DateTime CreatedAt { get; set; }

        public ICollection<Transaction> Transactions { get; set; } = [];
        public ICollection<Beneficiary> Beneficiaries { get; set; } = [];
    }
}
