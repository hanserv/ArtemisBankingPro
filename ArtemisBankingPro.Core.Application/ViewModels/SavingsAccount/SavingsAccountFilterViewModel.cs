using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class SavingsAccountFilterViewModel
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public SavingsAccountStatus? Status { get; set; } = SavingsAccountStatus.Active;
        public SavingsAccountType? Type { get; set; }
        public string? Identification { get; set; }
    }
}
