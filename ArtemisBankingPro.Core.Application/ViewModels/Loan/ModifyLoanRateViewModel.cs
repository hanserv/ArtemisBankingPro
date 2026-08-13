using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class ModifyLoanRateViewModel
    {
        [Required]
        public required int LoanId { get; set; }

        [Required(ErrorMessage = "The annual interest rate is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "The annual interest rate cannot be negative.")]
        public required decimal AnnualInterestRate { get; set; }
    }
}
