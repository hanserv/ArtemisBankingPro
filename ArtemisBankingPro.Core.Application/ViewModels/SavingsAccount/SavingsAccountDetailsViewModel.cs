using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class SavingsAccountDetailsViewModel
    {
        public required SavingsAccountViewModel Account { get; set; }
        public required PagedResult<TransactionViewModel> Transactions { get; set; }
    }
}
