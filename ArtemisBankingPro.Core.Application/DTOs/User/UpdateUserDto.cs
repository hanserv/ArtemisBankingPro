using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Fields required to update an existing user. Does not allow changing the user's role.
    /// </summary>
    public class UpdateUserDto : BaseDto<string>
    {
        /// <example>Orison</example>
        [SwaggerParameter(Description = "The user's first name.")]
        public required string FirstName { get; set; }

        /// <example>Soto</example>
        [SwaggerParameter(Description = "The user's last name.")]
        public required string LastName { get; set; }

        /// <example>00400300133</example>
        [SwaggerParameter(Description = "The user's identification number. Must be unique.")]
        public required string Identification { get; set; }

        /// <example>orison@artemis.com</example>
        [SwaggerParameter(Description = "The user's email address. Must be unique.")]
        public required string Email { get; set; }

        /// <example>orison01</example>
        [SwaggerParameter(Description = "The username used to log in. Must be unique.")]
        public required string UserName { get; set; }

        /// <example>Password123$</example>
        [SwaggerParameter(Description = "Optional. If sent, the user's password will be updated to this value.")]
        public string? Password { get; set; }

        /// <example>Password123$</example>
        [SwaggerParameter(Description = "Required if Password is sent. Must match the password field exactly.")]
        public string? ConfirmPassword { get; set; }

        /// <example>1000.00</example>
        [SwaggerParameter(Description = "Optional. Additional amount to credit to the user's principal savings account. Only applies to Client or Commerce roles.")]
        public decimal? AdditionalAmount { get; set; }
    }
}
