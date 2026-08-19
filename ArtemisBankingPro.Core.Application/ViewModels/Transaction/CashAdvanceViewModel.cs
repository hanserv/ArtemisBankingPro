using System.ComponentModel.DataAnnotations;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class CashAdvanceViewModel
    {
        [Required(ErrorMessage = "The credit card is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "The credit card is required.")]
        public required int CreditCardId { get; set; }

        [Required(ErrorMessage = "The destination account number is required.")]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "The account number must contain 9 digits.")]
        public required string DestinationAccountNumber { get; set; }

        [Required(ErrorMessage = "The advance amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The advance amount must be greater than zero.")]
        public required decimal Amount { get; set; }

        public List<CreditCardViewModel> CardOptions { get; set; } = [];
        public List<SavingsAccountViewModel> AccountOptions { get; set; } = [];
    }
}
