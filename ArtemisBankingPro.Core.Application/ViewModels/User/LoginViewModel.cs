using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "You must enter a UserName")]
        public required string UserName { get; set; }
        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
