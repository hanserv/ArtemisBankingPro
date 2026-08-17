using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.Beneficiary
{
    public class AddBeneficiaryViewModel
    {
        [Required(ErrorMessage = "The account number is required.")]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "The account number must contain exactly 9 digits.")]
        public required string AccountNumber { get; set; }
    }
}
