using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class AssignLoanViewModel
    {
        public required string ClientId { get; set; }

        [Required(ErrorMessage = "The loan term is required.")]
        public int TermInMonths { get; set; }

        [Required(ErrorMessage = "The loan amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The loan amount must be greater than zero.")]
        public decimal CapitalAmount { get; set; }

        [Required(ErrorMessage = "The annual interest rate is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "The annual interest rate cannot be negative.")]
        public decimal AnnualInterestRate { get; set; }
        public bool RiskWarningAccepted { get; set; } = false;
    }
}
