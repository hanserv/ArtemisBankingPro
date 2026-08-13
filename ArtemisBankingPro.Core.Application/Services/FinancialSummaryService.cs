using ArtemisBankingPro.Core.Application.DTOs.Loan;
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
        private readonly IBasicUserInfoService _basicUserInfoService;

        public FinancialSummaryService(ILoanRepository loanRepository, ICreditCardRepository creditCardRepository, 
            IBasicUserInfoService basicUserInfoService)
        {
            _loanRepository = loanRepository;
            _creditCardRepository = creditCardRepository;
            _basicUserInfoService = basicUserInfoService;
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

        public async Task<decimal> GetSystemAverageDebtAsync()
        {
            var activeClientIds = (await _basicUserInfoService.GetActiveClientsAsync(null))
                .Select(c => c.Id)
                .ToList();

            if (activeClientIds.Count == 0)
            {
                return 0m;
            }

            var totalLoanDebt = await _loanRepository.GetAllQuery()
                .Where(l => l.Status == LoanStatus.Active && activeClientIds.Contains(l.ClientId))
                .SumAsync(l => l.PendingAmount);

            var totalCreditCardDebt = await _creditCardRepository.GetAllQuery()
                .Where(c => c.Status == CreditCardStatus.Active && activeClientIds.Contains(c.ClientId))
                .SumAsync(c => c.CurrentDebt);

            var totalDebt = totalLoanDebt + totalCreditCardDebt;

            return Math.Round(totalDebt / activeClientIds.Count, 2, MidpointRounding.AwayFromZero);
        }

        public async Task<LoanRiskWarningDto?> CheckIfHighRiskAsync(string clientId, decimal additionalDebt)
        {
            var currentDebt = await GetTotalDebtByClientAsync(clientId);
            var averageDebt = await GetSystemAverageDebtAsync();
            var projectedDebt = currentDebt + additionalDebt;

            RiskType? riskType = currentDebt > averageDebt
                ? RiskType.CurrentHighRisk
                : projectedDebt > averageDebt
                    ? RiskType.ProjectedHighRisk
                    : null;

            if (riskType is null)
            {
                return null;
            }

            return new LoanRiskWarningDto
            {
                RiskType = riskType.Value,
                CurrentDebt = currentDebt,
                ProjectedDebt = projectedDebt,
                AverageDebt = averageDebt
            };
        }
    }
}
