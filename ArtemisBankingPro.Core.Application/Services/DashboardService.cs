using ArtemisBankingPro.Core.Application.DTOs.Dashboard;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ILoanRepository _loanRepository;
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IFinancialSummaryService _financialSummaryService;

        public DashboardService(IBasicUserInfoService basicUserInfoService, ITransactionRepository transactionRepository, 
            ISavingsAccountRepository savingsAccountRepository, ILoanRepository loanRepository,
            ICreditCardRepository creditCardRepository, IFinancialSummaryService financialSummaryService)
        {
            _basicUserInfoService = basicUserInfoService;
            _transactionRepository = transactionRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _loanRepository = loanRepository;
            _creditCardRepository = creditCardRepository;
            _financialSummaryService = financialSummaryService;
        }

        public async Task<Result<AdminDashboardDto>> GetAdminSummaryAsync()
        {
            var totalTransactions = await _transactionRepository.CountTransactionsAsync();
            var todayTransactions = await _transactionRepository.CountTransactionsAsync(DateTime.UtcNow.Date);
            var totalPayments = await _transactionRepository.CountPaymentsAsync();
            var todayPayments = await _transactionRepository.CountPaymentsAsync(DateTime.UtcNow.Date);

            var (activeClients, inactiveClients) = await _basicUserInfoService.GetClientStatusCountsAsync();

            var activeSavingsAccounts = await _savingsAccountRepository.CountActiveAsync();
            var activeLoans = await _loanRepository.CountActiveAsync();
            var activeCreditCards = await _creditCardRepository.CountActiveAsync();

            var averageDebt = await _financialSummaryService.GetSystemAverageDebtAsync();

            var dto = new AdminDashboardDto
            {
                TotalTransactions = totalTransactions,
                TodayTransactions = todayTransactions,
                TotalPayments = totalPayments,
                TodayPayments = todayPayments,
                ActiveClients = activeClients,
                InactiveClients = inactiveClients,
                TotalFinancialProducts = activeSavingsAccounts + activeLoans + activeCreditCards,
                ActiveLoans = activeLoans,
                ActiveCreditCards = activeCreditCards,
                ActiveSavingsAccounts = activeSavingsAccounts,
                AverageDebtPerClient = averageDebt
            };

            return Result<AdminDashboardDto>.Success(dto);
        }
    }
}
