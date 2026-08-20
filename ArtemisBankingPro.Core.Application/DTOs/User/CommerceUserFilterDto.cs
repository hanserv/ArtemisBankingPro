using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Filter parameters for retrieving the paginated list of Commerce-role users.
    /// </summary>
    public class CommerceUserFilterDto
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;
    }
}
