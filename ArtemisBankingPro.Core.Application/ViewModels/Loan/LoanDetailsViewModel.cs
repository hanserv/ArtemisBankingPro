namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class LoanDetailsViewModel
    {
        public required LoanViewModel Loan { get; set; }
        public required List<LoanInstallmentViewModel> Installments { get; set; }
    }
}
