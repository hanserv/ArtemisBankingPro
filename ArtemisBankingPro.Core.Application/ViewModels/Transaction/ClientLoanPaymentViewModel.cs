using System.ComponentModel.DataAnnotations;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ClientLoanPaymentViewModel
    {
        [Required(ErrorMessage = "The loan to pay is required.")]
        public required int LoanId { get; set; }

        [Required(ErrorMessage = "The source account is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The payment amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The payment amount must be greater than zero.")]
        public required decimal Amount { get; set; }

        public List<LoanViewModel> LoanOptions { get; set; } = [];
        public List<SavingsAccountViewModel> SourceAccountOptions { get; set; } = [];
    }
}
