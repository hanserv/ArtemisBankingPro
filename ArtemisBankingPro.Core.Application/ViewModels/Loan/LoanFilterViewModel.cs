using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class LoanFilterViewModel
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public LoanStatus? Status { get; set; } = LoanStatus.Active;
        public string? Identification { get; set; }
    }
}
