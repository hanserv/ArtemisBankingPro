namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class ModifyLoanRateDto
    {
        public required int LoanId { get; set; }
        public required decimal AnnualInterestRate { get; set; }
    }
}
