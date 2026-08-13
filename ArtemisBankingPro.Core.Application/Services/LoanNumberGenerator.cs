using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class LoanNumberGenerator : ILoanNumberGenerator
    {
        private const int MaxAttempts = 25;

        private readonly ILoanRepository _loanRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;

        public LoanNumberGenerator(ILoanRepository loanRepository, ISavingsAccountRepository savingsAccountRepository)
        {
            _loanRepository = loanRepository;
            _savingsAccountRepository = savingsAccountRepository;
        }

        public async Task<string> GenerateAsync()
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var candidate = NumericStringGenerator.Generate(9);

                var existsAsLoan = await _loanRepository.LoanNumberExistsAsync(candidate);
                var existsAsAccount = await _savingsAccountRepository.AccountNumberExistsAsync(candidate);

                if (!existsAsLoan && !existsAsAccount)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Could not generate a unique 9-digit loan number after several attempts.");
        }
    }

}
