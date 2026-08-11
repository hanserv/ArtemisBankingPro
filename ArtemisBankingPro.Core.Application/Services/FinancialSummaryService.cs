using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class FinancialSummaryService : IFinancialSummaryService
    {
        private readonly ILoanRepository _loanRepository;
        private readonly ICreditCardRepository _creditCardRepository;

        public FinancialSummaryService(ILoanRepository loanRepository, ICreditCardRepository creditCardRepository)
        {
            _loanRepository = loanRepository;
            _creditCardRepository = creditCardRepository;
        }

        public async Task<decimal> GetTotalDebtByClientAsync(string clientId)
        {
            var loanDebt = await _loanRepository.GetAllQuery()
                .Where(l => l.ClientId == clientId && l.Status == LoanStatus.Active)
                .SumAsync(l => l.PendingAmount);

            var creditCardDebt = await _creditCardRepository.GetAllQuery()
                .Where(c => c.ClientId == clientId && c.Status == CreditCardStatus.Active)
                .SumAsync(c => c.CurrentDebt);

            return loanDebt + creditCardDebt;
        }

        public Task<bool> CheckIfHighRiskAsync(string clientId, decimal additionalDebt)
        {
            throw new NotImplementedException();
        }

        public Task<decimal> GetSystemAverageDebtAsync()
        {
            throw new NotImplementedException();
        }
    }
}
