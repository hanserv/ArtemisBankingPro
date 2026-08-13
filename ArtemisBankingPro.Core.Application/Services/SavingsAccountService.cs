using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class SavingsAccountService : ISavingsAccountService
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountNumberGenerator _accountNumberGenerator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;
        private readonly IFinancialSummaryService _financialSummaryService;
        private readonly ILogger<SavingsAccountService> _logger;

        public SavingsAccountService(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IAccountNumberGenerator accountNumberGenerator, IUnitOfWork unitOfWork,
            IBasicUserInfoService basicUserInfoService, IMapper mapper,
            IFinancialSummaryService financialSummaryService, ILogger<SavingsAccountService> logger)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _accountNumberGenerator = accountNumberGenerator;
            _unitOfWork = unitOfWork;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
            _financialSummaryService = financialSummaryService;
            _logger = logger;
        }

        public async Task<Result<SavingsAccountDto>> GetByIdAsync(int id)
        {
            var account = await _savingsAccountRepository.GetByIdAsync(id);

            if (account is null)
            {
                return Result<SavingsAccountDto>.Failure(error: "The selected account does not exist.");
            }

            var dto = _mapper.Map<SavingsAccountDto>(account);
            dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(account.ClientId);

            return Result<SavingsAccountDto>.Success(dto);
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
                        Category = TransactionCategory.AccountOpening,
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
                    Category = TransactionCategory.AdministrativeAdjustment,
                    Origin = "Administrative Adjustment",
                    Beneficiary = account.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = performedByUserId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            return Result.Success(message: "The additional amount has been credited successfully.");
        }

        public async Task<Result<PagedResult<SavingsAccountDto>>> GetPagedAsync(SavingsAccountFilterDto filter)
        {
            if (filter.Page <= 0)
            {
                return Result<PagedResult<SavingsAccountDto>>.Failure(error: "The page parameter must be greater than zero.");
            }

            if (filter.PageSize <= 0)
            {
                return Result<PagedResult<SavingsAccountDto>>.Failure(error: "The pageSize parameter must be greater than zero.");
            }

            if (filter.PageSize > 20)
            {
                filter.PageSize = 20;
            }

            string? clientId = null;

            if (!string.IsNullOrWhiteSpace(filter.Identification))
            {
                clientId = await _basicUserInfoService.GetUserIdByIdentificationAsync(filter.Identification);
                if (clientId is null)
                {
                    return Result<PagedResult<SavingsAccountDto>>.Failure(error: "There is no client registered with this identification.");
                }
            }

            var query = _savingsAccountRepository.GetAllQuery();

            if (filter.Status is not null)
            {
                query = query.Where(a => a.Status == filter.Status);
            }

            if (filter.Type is not null)
            {
                query = query.Where(a => a.Type == filter.Type);
            }

            if (clientId is not null)
            {
                query = query.Where(a => a.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync();

            if (clientId is not null && totalRecords == 0)
            {
                return Result<PagedResult<SavingsAccountDto>>.Failure(error: "This client has no savings accounts registered.");
            }

            var orderedQuery = clientId is not null && filter.Status is null
                ? query.OrderBy(a => a.Status == SavingsAccountStatus.Cancelled).ThenByDescending(a => a.CreatedAt)
                : query.OrderByDescending(a => a.CreatedAt);

            var accounts = await orderedQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var items = new List<SavingsAccountDto>();
            foreach (var account in accounts)
            {
                var dto = _mapper.Map<SavingsAccountDto>(account);
                dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(account.ClientId);
                items.Add(dto);
            }

            return Result<PagedResult<SavingsAccountDto>>.Success(new PagedResult<SavingsAccountDto>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalRecords = totalRecords
            });
        }

        public async Task<Result> ValidateClientForAssignmentAsync(string? clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Result.Failure(error: "You must select a client to continue.");
            }

            var isActive = await _basicUserInfoService.IsClientActiveAsync(clientId);

            if (isActive is null or false)
            {
                return Result.Failure(error: "Savings accounts can only be assigned to active clients.");
            }

            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(clientId);

            if (principalAccount is null || principalAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure(error: "The client must have an active principal savings account before a secondary account can be assigned.");
            }

            return Result.Success();
        }

        public async Task<Result> CreateSecondaryAccountAsync(string clientId, decimal initialBalance, string createdByAdminId)
        {
            _logger.LogInformation("Administrator {AdminId} initiated secondary account assignment for client {ClientId} with initial balance {InitialBalance:C}.",createdByAdminId, clientId, initialBalance);

            if (initialBalance < 0)
            {
                return Result.Failure(error: "The initial balance cannot be negative.");
            }

            var validation = await ValidateClientForAssignmentAsync(clientId);
            if (!validation.IsSuccess)
            {
                _logger.LogWarning("Secondary account assignment rejected for client {ClientId}: {Error}.", clientId, validation.Error);

                return validation;
            }

            var accountNumber = await _accountNumberGenerator.GenerateAsync();
            var hasInitialCredit = initialBalance > 0;

            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = initialBalance,
                Type = SavingsAccountType.Secondary,
                Status = SavingsAccountStatus.Active,
                CreatedByAdminId = createdByAdminId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _savingsAccountRepository.AddAsync(account);

                if (hasInitialCredit)
                {
                    await _transactionRepository.AddAsync(new Transaction
                    {
                        Id = 0,
                        SavingsAccountId = account.Id,
                        Amount = initialBalance,
                        Type = TransactionType.Credit,
                        Category = TransactionCategory.AccountOpening,
                        Origin = "Account Opening",
                        Beneficiary = account.AccountNumber,
                        Status = TransactionStatus.Approved,
                        PerformedByUserId = createdByAdminId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            });

            _logger.LogInformation(hasInitialCredit
                ? "Secondary account {AccountNumber} assigned to client {ClientId} by administrator {AdminId}, opened with an initial credit of {InitialBalance:C}."
                : "Secondary account {AccountNumber} assigned to client {ClientId} by administrator {AdminId} with zero balance.", account.AccountNumber, clientId, createdByAdminId, initialBalance);


            return Result.Success(message: "The secondary savings account has been created successfully.");
        }

        public async Task<Result> CancelSecondaryAccountAsync(int accountId, string performedByAdminId)
        {
            _logger.LogInformation("Administrator {AdminId} initiated cancellation of account {AccountId}.", performedByAdminId, accountId);

            var account = await _savingsAccountRepository.GetByIdAsync(accountId);
            if (account is null)
            {
                return Result.Failure(error: "The selected account does not exist.");
            }

            if (account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure(error: "The selected account is already cancelled.");
            }

            if (account.Type != SavingsAccountType.Secondary)
            {
                return Result.Failure(error: "Principal accounts cannot be cancelled.");
            }

            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(account.ClientId);
            if (principalAccount is null)
            {
                _logger.LogWarning("Cancellation of secondary account {AccountNumber} failed: client {ClientId} has no active principal account to receive the funds.", account.AccountNumber, account.ClientId);
                return Result.Failure(error: "It is not possible to cancel the account because the client does not have an active principal account to receive the funds.");
            }

            var transferredAmount = account.Balance;
            var hasBalanceToTransfer = transferredAmount > 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (hasBalanceToTransfer)
                {
                    var amountToTransfer = account.Balance;

                    await _transactionRepository.AddAsync(new Transaction
                    {
                        Id = 0,
                        SavingsAccountId = account.Id,
                        Amount = amountToTransfer,
                        Type = TransactionType.Debit,
                        Category = TransactionCategory.Transfer,
                        Origin = account.AccountNumber,
                        Beneficiary = principalAccount.AccountNumber,
                        Status = TransactionStatus.Approved,
                        PerformedByUserId = performedByAdminId,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _transactionRepository.AddAsync(new Transaction
                    {
                        Id = 0,
                        SavingsAccountId = principalAccount.Id,
                        Amount = amountToTransfer,
                        Type = TransactionType.Credit,
                        Category = TransactionCategory.Transfer,
                        Origin = account.AccountNumber,
                        Beneficiary = principalAccount.AccountNumber,
                        Status = TransactionStatus.Approved,
                        PerformedByUserId = performedByAdminId,
                        CreatedAt = DateTime.UtcNow
                    });

                    account.Balance = 0;
                    principalAccount.Balance += amountToTransfer;
                    await _savingsAccountRepository.UpdateAsync(principalAccount);
                }

                account.Status = SavingsAccountStatus.Cancelled;
                await _savingsAccountRepository.UpdateAsync(account);
            });

            if (hasBalanceToTransfer)
            {
                _logger.LogInformation("Transfer of {TransferredAmount:C} completed from {FromAccount} to {ToAccount} as part of cancellation.", transferredAmount, account.AccountNumber, principalAccount.AccountNumber);
            }

            _logger.LogInformation("Secondary account {AccountNumber} cancelled by administrator {AdminId}.", account.AccountNumber, performedByAdminId);

            return Result.Success(message: "The secondary savings account has been cancelled successfully.");
        }
    }
}
