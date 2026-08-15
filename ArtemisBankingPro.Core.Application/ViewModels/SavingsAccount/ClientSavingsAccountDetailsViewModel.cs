using ArtemisBankingPro.Core.Application.ViewModels.Transaction;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class ClientSavingsAccountDetailsViewModel
    {
        public required SavingsAccountViewModel Account { get; set; }
        public required List<TransactionViewModel> Transactions { get; set; }
    }
}
