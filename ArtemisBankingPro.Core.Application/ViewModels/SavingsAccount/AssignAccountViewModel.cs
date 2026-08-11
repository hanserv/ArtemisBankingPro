using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class AssignAccountViewModel
    {
        [Required]
        public required string ClientId { get; set; }

        [Required(ErrorMessage = "The initial balance is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "The initial balance cannot be negative.")]
        public decimal InitialBalance { get; set; }
    }
}
