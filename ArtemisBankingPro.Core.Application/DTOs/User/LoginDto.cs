using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Parameters required to authenticate a user
    /// </summary>
    public class LoginDto
    {
        /// <example>admin</example>
        [SwaggerParameter(Description = "The username registered in the system")]
        public required string UserName { get; set; }

        /// <example>Password123$</example>
        [SwaggerParameter(Description = "The password associated with the user")]
        public required string Password { get; set; }
    }
}
