using System.ComponentModel.DataAnnotations;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ExpressTransactionViewModel
    {
        [Required(ErrorMessage = "The source account is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The destination account number is required.")]
        public required string DestinationAccountNumber { get; set; }

        [Required(ErrorMessage = "The amount to transfer is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The amount to transfer must be greater than zero.")]
        public required decimal Amount { get; set; }

        public List<SavingsAccountViewModel> SourceAccountOptions { get; set; } = [];
    }
}
