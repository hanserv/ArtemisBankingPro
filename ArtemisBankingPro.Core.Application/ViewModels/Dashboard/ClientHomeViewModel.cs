using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Dashboard
{
    public class ClientHomeViewModel
    {
        public required List<SavingsAccountViewModel> SavingsAccounts { get; set; }
        public required List<LoanViewModel> Loans { get; set; }
        public required List<CreditCardViewModel> CreditCards { get; set; }

        public bool HasAnyProduct => SavingsAccounts.Count > 0 || Loans.Count > 0 || CreditCards.Count > 0;
    }
}
