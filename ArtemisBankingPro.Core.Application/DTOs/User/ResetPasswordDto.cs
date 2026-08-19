using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Parameters required to reset the password of a user account
    /// </summary>
    public class ResetPasswordDto
    {
        /// <example>966d4086-da37-49f9-b311-b95be0729db8</example>
        [SwaggerParameter(Description = "The id of the user whose password will be reset")]
        public required string UserId { get; set; }
        /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9</example>
        [SwaggerParameter(Description = "Password reset token sent to the user's registered email address")]
        public required string Token { get; set; }
        /// <example>Password123!</example>
        [SwaggerParameter(Description = "The new password for the user account.")]
        public required string Password { get; set; }
        /// <example>Password123!</example>
        [SwaggerParameter(Description = "Confirmation of the new password. Must match the Password field.")]
        public required string ConfirmPassword { get; set; }
    }
}
