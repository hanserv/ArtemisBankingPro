using System.ComponentModel.DataAnnotations;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "The first name is required.")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "The last name is required.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "The identification is required.")]
        public required string Identification { get; set; }

        [Required(ErrorMessage = "The email is required.")]
        [EmailAddress(ErrorMessage = "The email must have a valid format.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "The username is required.")]
        public required string UserName { get; set; }

        [Required(ErrorMessage = "The password is required.")]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Required(ErrorMessage = "The password confirmation is required.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and the password confirmation must match.")]
        public required string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "The role is required.")]
        public required UserType UserType { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "The initial amount cannot be negative.")]
        public decimal? InitialAmount { get; set; }
    }
}
