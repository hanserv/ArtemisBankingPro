using ArtemisBankingPro.Core.Domain.Common.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    /// <summary>
    /// Filter parameters for retrieving the paginated list of users.
    /// </summary>
    public class UserFilterDto
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        /// <example>Admin</example>
        [SwaggerParameter(Description = "Optional role filter. Allowed values: Admin, Cashier or Client. Users with the Commerce role are not included.")]
        public UserType? Role { get; set; }
    }
}
