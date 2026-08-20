using ArtemisBankingPro.Core.Domain.Common.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class LoanFilterDto
    {
        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;

        /// <example>Active</example>
        [SwaggerParameter(Description = "Optional status filter.")]
        public LoanStatus? Status { get; set; } = LoanStatus.Active;

        [SwaggerParameter(Description = "Optional client identification to search loans for a specific client.")]
        public string? Identification { get; set; }
    }
}
