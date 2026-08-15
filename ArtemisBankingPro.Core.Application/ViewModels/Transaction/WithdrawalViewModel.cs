using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class WithdrawalViewModel
    {
        [Required(ErrorMessage = "The source account number is required.")]
        public required string AccountNumber { get; set; }

        [Required(ErrorMessage = "The withdrawal amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The withdrawal amount must be greater than zero.")]
        public required decimal Amount { get; set; }
    }
}
