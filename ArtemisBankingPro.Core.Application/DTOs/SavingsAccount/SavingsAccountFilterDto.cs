using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.SavingsAccount
{
    public class SavingsAccountFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public SavingsAccountStatus? Status { get; set; }
        public SavingsAccountType? Type { get; set; }
        public string? Identification { get; set; }
    }
}
