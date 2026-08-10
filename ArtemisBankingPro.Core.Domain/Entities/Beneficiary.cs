using ArtemisBankingPro.Core.Domain.Common;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class Beneficiary : BaseEntity<int>
    {
        public required string ClientId { get; set; }

        public required int SavingsAccountId { get; set; }
        public SavingsAccount? SavingsAccount { get; set; }

        public required DateTime CreatedAt { get; set; }
    }
}
