using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Parameters required to get a reset token for a user with role admin or commerce
    /// </summary>
    public class RequestPasswordResetDto
    {
        /// <example>admin</example>
        [SwaggerParameter(Description = "The username registered in the system")]
        public required string UserName { get; set; }
    }
}
