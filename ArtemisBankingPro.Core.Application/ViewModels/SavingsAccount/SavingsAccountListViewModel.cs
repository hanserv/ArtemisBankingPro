using ArtemisBankingPro.Core.Application.DTOs;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class SavingsAccountListViewModel
    {
        public required SavingsAccountFilterViewModel Filter { get; set; }
        public required PagedResult<SavingsAccountViewModel> Accounts { get; set; }
    }
}
