namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class AssignLoanDto
    {
        public required string ClientId { get; set; }
        public required decimal CapitalAmount { get; set; }
        public required decimal AnnualInterestRate { get; set; }
        public required int TermInMonths { get; set; }
        public required string AdminId { get; set; }
        public bool RiskWarningAccepted { get; set; } = false;
    }
}
