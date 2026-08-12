using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class ModifyCreditCardLimitViewModel
    {
        [Required]
        public required int CreditCardId { get; set; }

        [Required(ErrorMessage = "The credit limit is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The credit limit must be greater than zero.")]
        public decimal CreditLimit { get; set; }
    }
}
