using System.ComponentModel.DataAnnotations;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ClientCreditCardPaymentViewModel
    {
        [Required(ErrorMessage = "The destination credit card is required.")]
        public required int CreditCardId { get; set; }

        [Required(ErrorMessage = "The source account is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The payment amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The payment amount must be greater than zero.")]
        public required decimal Amount { get; set; }

        public List<CreditCardViewModel> CardOptions { get; set; } = [];
        public List<SavingsAccountViewModel> SourceAccountOptions { get; set; } = [];
    }
}
