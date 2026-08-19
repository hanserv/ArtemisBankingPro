using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Parameters required to confirm a user account
    /// </summary>
    public class ConfirmAccountDto
    {
        /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9</example>
        [SwaggerParameter(Description = "The confirmation token sent to the user's email")]
        public required string Token { get; set; }
    }
}
