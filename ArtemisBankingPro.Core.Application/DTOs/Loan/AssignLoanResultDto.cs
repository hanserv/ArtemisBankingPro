namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class AssignLoanResultDto
    {
        public bool RequiresRiskConfirmation { get; set; }
        public LoanRiskWarningDto? RiskWarning { get; set; }
        public LoanDto? Loan { get; set; }
    }
}
