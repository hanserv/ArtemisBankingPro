using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class CreditCardPaymentViewModel
    {
        [Required(ErrorMessage = "The source account number is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The card number is required.")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "The card number must contain 16 digits.")]
        public required string CardNumber { get; set; }

        [Required(ErrorMessage = "The payment amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The payment amount must be greater than zero.")]
        public required decimal Amount { get; set; }
    }
}
