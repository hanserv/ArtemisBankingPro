using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class SavingsAccountService : ISavingsAccountService
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountNumberGenerator _accountNumberGenerator;
        private readonly IUnitOfWork _unitOfWork;

        public SavingsAccountService(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IAccountNumberGenerator accountNumberGenerator, IUnitOfWork unitOfWork)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _accountNumberGenerator = accountNumberGenerator;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> CreatePrincipalAccountAsync(string clientId, decimal initialAmount)
        {
            if (initialAmount < 0)
            {
                return Result.Failure(error: "The initial amount cannot be negative.");
            }

            var accountNumber = await _accountNumberGenerator.GenerateAsync();

            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = initialAmount, 
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _savingsAccountRepository.AddAsync(account);

                if (initialAmount > 0)
                {
                    await _transactionRepository.AddAsync(new Transaction
                    {
                        Id = 0,
                        SavingsAccountId = account.Id,
                        Amount = initialAmount,
                        Type = TransactionType.Credit,
                        Origin = "Account Opening",
                        Beneficiary = account.AccountNumber,
                        Status = TransactionStatus.Approved,
                        PerformedByUserId = null, 
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            });

            return Result.Success(message: "The principal savings account has been created successfully.");
        }

        public async Task<Result> CreditAdditionalAmountAsync(string clientId, decimal amount, string performedByUserId)
        {
            var account = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(clientId);
            if (account is null)
            {
                return Result.Failure(error: "The client does not have a principal savings account.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                account.Balance += amount;
                await _savingsAccountRepository.UpdateAsync(account);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = account.Id,
                    Amount = amount,
                    Type = TransactionType.Credit,
                    Origin = "Administrative Adjustment",
                    Beneficiary = account.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = performedByUserId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            return Result.Success(message: "The additional amount has been credited successfully.");
        }
    }
}
