namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class LoanDetailsDto
    {
        public required LoanDto Loan { get; set; }
        public required List<LoanInstallmentDto> Installments { get; set; }
    }
}
