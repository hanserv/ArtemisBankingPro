using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class AccountNumberGenerator : IAccountNumberGenerator
    {
        private const int MaxAttempts = 25;

        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ILoanRepository _loanRepository;

        public AccountNumberGenerator(ISavingsAccountRepository savingsAccountRepository, ILoanRepository loanRepository)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _loanRepository = loanRepository;
        }

        public async Task<string> GenerateAsync()
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var candidate = NumericStringGenerator.Generate(9);

                var existsAsAccount = await _savingsAccountRepository.AccountNumberExistsAsync(candidate);
                var existsAsLoan = await _loanRepository.LoanNumberExistsAsync(candidate);

                if (!existsAsAccount && !existsAsLoan)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Could not generate a unique 9-digit number after several attempts.");
        }
    }
}
