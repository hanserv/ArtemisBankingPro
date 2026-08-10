using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class LoanInstallment : BaseEntity<int>
    {
        public required int InstallmentNumber { get; set; }
        public required DateTime DueDate { get; set; }
        public required decimal InstallmentAmount { get; set; } 
        public required decimal InterestAmount { get; set; }
        public required decimal PrincipalAmount { get; set; }
        public required decimal RemainingBalance { get; set; } // will be "pendingInstallmentAmount" in dto
        public required InstallmentStatus Status { get; set; }
        public bool IsLate { get; set; }

        public required int LoanId { get; set; }
        public Loan? Loan { get; set; }
    }
}
