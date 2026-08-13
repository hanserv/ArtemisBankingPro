using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class LoanInstallmentDto
    {
        public required int InstallmentNumber { get; set; }
        public required DateTime DueDate { get; set; }
        public required decimal InstallmentAmount { get; set; }
        public required decimal InterestAmount { get; set; }
        public required decimal CapitalAmount { get; set; } 
        public required decimal PendingInstallmentAmount { get; set; } 
        public required InstallmentStatus PaymentStatus { get; set; } 
        public required bool IsLate { get; set; }
    }
}
