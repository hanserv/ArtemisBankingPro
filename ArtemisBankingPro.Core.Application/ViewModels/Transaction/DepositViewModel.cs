using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class DepositViewModel
    {
        [Required(ErrorMessage = "The destination account number is required.")]
        public required string AccountNumber { get; set; }

        [Required(ErrorMessage = "The deposit amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The deposit amount must be greater than zero.")]
        public required decimal Amount { get; set; }
    }
}
