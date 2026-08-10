using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class RequestPasswordResetViewModel
    {
        [Required(ErrorMessage = "You must enter a UserName")]
        [DataType(DataType.Text)]
        public required string UserName { get; set; }
    }
}
