using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Dashboard;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;

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
        private readonly IMapper _mapper;

        public DashboardService(IBasicUserInfoService basicUserInfoService, ITransactionRepository transactionRepository,
            ISavingsAccountRepository savingsAccountRepository, ILoanRepository loanRepository,
            ICreditCardRepository creditCardRepository, IFinancialSummaryService financialSummaryService, 
            IMapper mapper)
        {
            _basicUserInfoService = basicUserInfoService;
            _transactionRepository = transactionRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _loanRepository = loanRepository;
            _creditCardRepository = creditCardRepository;
            _financialSummaryService = financialSummaryService;
            _mapper = mapper;
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

        public async Task<Result<CashierDashboardDto>> GetCashierSummaryAsync(string cashierId)
        {
            var today = DateTime.UtcNow.Date;

            var todayTransactions = await _transactionRepository.CountTransactionsAsync(today, cashierId, onlyApproved: true);

            var todayPayments = await _transactionRepository.CountPaymentsAsync(today, cashierId);

            var todayDeposits = await _transactionRepository.CountByCategoryAsync(TransactionCategory.Deposit, today, cashierId);

            var todayWithdrawals = await _transactionRepository.CountByCategoryAsync(TransactionCategory.Withdrawal, today, cashierId);

            var dto = new CashierDashboardDto
            {
                TodayTransactions = todayTransactions,
                TodayPayments = todayPayments,
                TodayDeposits = todayDeposits,
                TodayWithdrawals = todayWithdrawals
            };

            return Result<CashierDashboardDto>.Success(dto);
        }

        public async Task<Result<ClientProductsDto>> GetClientProductsAsync(string clientId)
        {
            var accounts = await _savingsAccountRepository.GetActiveByClientIdAsync(clientId);
            var loans = await _loanRepository.GetActiveByClientIdAsync(clientId);
            var cards = await _creditCardRepository.GetActiveByClientIdAsync(clientId);

            return Result<ClientProductsDto>.Success(new ClientProductsDto
            {
                SavingsAccounts = _mapper.Map<List<SavingsAccountDto>>(accounts),
                Loans = _mapper.Map<List<LoanDto>>(loans),
                CreditCards = _mapper.Map<List<CreditCardDto>>(cards)
            });
        }
    }
}
