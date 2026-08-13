using ArtemisBankingPro.Core.Application.DTOs;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class LoanListViewModel
    {
        public required LoanFilterViewModel Filter { get; set; }
        public required PagedResult<LoanViewModel> Loans { get; set; }
    }
}
