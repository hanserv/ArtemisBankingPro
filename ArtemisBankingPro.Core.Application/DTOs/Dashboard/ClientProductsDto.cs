using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.DTOs.Dashboard
{
    public class ClientProductsDto
    {
        public required List<SavingsAccountDto> SavingsAccounts { get; set; }
        public required List<LoanDto> Loans { get; set; }
        public required List<CreditCardDto> CreditCards { get; set; }
    }
}
