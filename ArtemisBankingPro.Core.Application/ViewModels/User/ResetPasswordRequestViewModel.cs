using System.ComponentModel.DataAnnotations;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class ResetPasswordRequestViewModel
    {
        [Required(ErrorMessage = "Id is required.")]
        [DataType(DataType.Text)]
        public required string UserId { get; set; }
        [Required(ErrorMessage = "Token is required.")]
        [DataType(DataType.Text)]
        public required string Token { get; set; }
        [Required(ErrorMessage = "You must enter a {0}.")]
        [DataType(DataType.Password)]
        public required string Password { get; set; }
        [Compare(nameof(Password), ErrorMessage = "The password and password confirmation do not match.")]
        [DataType(DataType.Password)]
        public required string ConfirmPassword { get; set; }
    }
}
