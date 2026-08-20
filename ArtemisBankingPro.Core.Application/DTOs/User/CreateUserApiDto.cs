using ArtemisBankingPro.Core.Domain.Common.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Parameters required to create a new Administrator, Cashier or Client user.
    /// </summary>
    public class CreateUserApiDto
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
        [SwaggerParameter(Description = "The user's initial password.")]
        public required string Password { get; set; }

        /// <example>Password123$</example>
        [SwaggerParameter(Description = "Must match the password field exactly.")]
        public required string ConfirmPassword { get; set; }

        /// <example>Client</example>
        [SwaggerParameter(Description = "The user's role. Allowed values: Admin, Cashier or Client. Cannot be used to create Commerce users.")]
        public required UserType Role { get; set; }

        /// <example>5000.00</example>
        [SwaggerParameter(Description = "Initial balance for the principal savings account. Only applies when Role is Client. Defaults to RD$0.00 if not sent.")]
        public decimal? InitialAmount { get; set; }
    }
}
