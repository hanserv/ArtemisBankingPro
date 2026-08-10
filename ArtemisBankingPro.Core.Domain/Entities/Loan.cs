using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class Loan : BaseEntity<int>
    {
        public required string LoanNumber { get; set; }
        public required string ClientId { get; set; }
        public required decimal CapitalAmount { get; set; }
        public required decimal PendingAmount { get; set; }
        public required decimal AnnualInterestRate { get; set; }
        public required int TermInMonths { get; set; }
        public required string CreatedByAdminId { get; set; }
        public required LoanStatus Status { get; set; }
        public required DateTime CreatedAt { get; set; }

        public ICollection<LoanInstallment> Installments { get; set; } = [];
    }
}
