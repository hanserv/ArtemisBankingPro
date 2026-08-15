using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ThirdPartyTransactionViewModel
    {
        [Required(ErrorMessage = "The source account number is required.")]
        public required string SourceAccountNumber { get; set; }

        [Required(ErrorMessage = "The destination account number is required.")]
        public required string DestinationAccountNumber { get; set; }

        [Required(ErrorMessage = "The transaction amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The transaction amount must be greater than zero.")]
        public required decimal Amount { get; set; }
    }
}
