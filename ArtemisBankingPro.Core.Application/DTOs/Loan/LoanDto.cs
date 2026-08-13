using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class LoanDto : BaseDto<int>
    {
        public required string LoanNumber { get; set; }
        public required string ClientId { get; set; }
        public required string ClientFullName { get; set; }
        public required decimal CapitalAmount { get; set; }
        public required decimal MonthlyInstallment { get; set; }
        public required int TotalInstallments { get; set; }
        public required int PaidInstallments { get; set; }
        public required decimal PendingAmount { get; set; }
        public required decimal AnnualInterestRate { get; set; }
        public required int TermInMonths { get; set; }
        public required LoanStatus Status { get; set; }
        public required ClientPaymentStatus ClientPaymentStatus { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
