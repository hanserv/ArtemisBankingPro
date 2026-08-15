using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class LoanPaymentViewModel
    {
        [Required(ErrorMessage = "The source account number is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The loan number is required.")]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "The loan number must contain 9 digits.")]
        public required string LoanNumber { get; set; }

        [Required(ErrorMessage = "The payment amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The payment amount must be greater than zero.")]
        public required decimal Amount { get; set; }
    }
}
