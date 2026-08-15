using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly IMapper _mapper;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TransactionService> _logger;
        private readonly ICreditCardRepository _creditCardRepository;


        public TransactionService(ITransactionRepository transactionRepository, ISavingsAccountRepository savingsAccountRepository,
            IMapper mapper, IBasicUserInfoService basicUserInfoService,
            IEmailService emailService, IUnitOfWork unitOfWork,
            ILogger<TransactionService> logger, ICreditCardRepository creditCardRepository)
        {
            _transactionRepository = transactionRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _mapper = mapper;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _creditCardRepository = creditCardRepository;
        }

        public async Task<Result<PagedResult<TransactionDto>>> GetAccountTransactionsAsync(int accountId, int page, int pageSize)
        {
            if (page <= 0)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The page parameter must be greater than zero.");
            }

            if (pageSize <= 0)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The pageSize parameter must be greater than zero.");
            }

            if (pageSize > 20)
            {
                pageSize = 20;
            }

            var accountExists = await _savingsAccountRepository.GetByIdAsync(accountId) is not null;

            if (!accountExists)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The selected account does not exist.");
            }

            var query = _transactionRepository.GetAllQuery()
                .Where(t => t.SavingsAccountId == accountId);

            var totalRecords = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = _mapper.Map<List<TransactionDto>>(transactions);

            return Result<PagedResult<TransactionDto>>.Success(new PagedResult<TransactionDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }

        public async Task<Result<DepositConfirmationDto>> ValidateDepositAsync(DepositDto dto)
        {
            if (dto.Amount <= 0)
            {
                return Result<DepositConfirmationDto>.Failure("The deposit amount must be greater than zero.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.AccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result<DepositConfirmationDto>.Failure("The account number entered does not correspond to a valid account.");
            }

            var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);

            return Result<DepositConfirmationDto>.Success(new DepositConfirmationDto
            {
                AccountNumber = account.AccountNumber,
                AccountHolderName = client!.FullName,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmDepositAsync(DepositConfirmationDto dto, string cashierId)
        {
            if (dto.Amount <= 0)
            {
                return Result.Failure("The deposit amount must be greater than zero.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.AccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The account number entered does not correspond to a valid account.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                account.Balance += dto.Amount;
                await _savingsAccountRepository.UpdateAsync(account);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = account.Id,
                    Amount = dto.Amount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.Deposit,
                    Origin = "DEPOSIT",
                    Beneficiary = account.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = cashierId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Deposit of {Amount:C} made to account ending in {LastFourDigits} by cashier {CashierId}.", dto.Amount, account.AccountNumber[^4..], cashierId);

            var emailSent = await TrySendDepositEmailAsync(account, dto.Amount);

            return Result.Success(emailSent
                ? "The deposit was completed successfully."
                : "The deposit was completed successfully, but the notification email could not be sent.");
        }

        public async Task<Result<WithdrawalConfirmationDto>> ValidateWithdrawalAsync(WithdrawalDto dto, string cashierId)
        {
            if (dto.Amount <= 0)
            {
                return Result<WithdrawalConfirmationDto>.Failure("The withdrawal amount must be greater than zero.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.AccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result<WithdrawalConfirmationDto>.Failure("The account number entered does not correspond to a valid account.");
            }

            if (account.Balance < dto.Amount)
            {
                await LogRejectedWithdrawalAsync(account, dto.Amount, cashierId);
                return Result<WithdrawalConfirmationDto>.Failure("The amount entered exceeds the account's available balance.");
            }

            var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);

            return Result<WithdrawalConfirmationDto>.Success(new WithdrawalConfirmationDto
            {
                AccountNumber = account.AccountNumber,
                AccountHolderName = client!.FullName,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmWithdrawalAsync(WithdrawalConfirmationDto dto, string cashierId)
        {
            if (dto.Amount <= 0)
            {
                return Result.Failure("The withdrawal amount must be greater than zero.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.AccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The account number entered does not correspond to a valid account.");
            }

            if (account.Balance < dto.Amount)
            {
                await LogRejectedWithdrawalAsync(account, dto.Amount, cashierId);
                return Result.Failure("The amount entered exceeds the account's available balance.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                account.Balance -= dto.Amount;
                await _savingsAccountRepository.UpdateAsync(account);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = account.Id,
                    Amount = dto.Amount,
                    Type = TransactionType.Debit,
                    Category = TransactionCategory.Withdrawal,
                    Origin = account.AccountNumber,
                    Beneficiary = "WITHDRAWAL",
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = cashierId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Withdrawal of {Amount:C} made from account ending in {LastFourDigits} by cashier {CashierId}.", dto.Amount, account.AccountNumber[^4..], cashierId);

            var emailSent = await TrySendWithdrawalEmailAsync(account, dto.Amount);

            return Result.Success(emailSent
                ? "The withdrawal was completed successfully."
                : "The withdrawal was completed successfully, but the notification email could not be sent.");
        }

        public async Task<Result<CreditCardPaymentConfirmationDto>> ValidateCreditCardPaymentAsync(CreditCardPaymentDto dto, string cashierId)
        {
            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result<CreditCardPaymentConfirmationDto>.Failure("The account number entered does not correspond to a valid account.");
            }

            if (dto.CardNumber.Length != 16 || !dto.CardNumber.All(char.IsDigit))
            {
                return Result<CreditCardPaymentConfirmationDto>.Failure("The card number must contain 16 digits.");
            }

            var card = await _creditCardRepository.GetByCardNumberAsync(dto.CardNumber);
            if (card is null || card.Status != CreditCardStatus.Active)
            {
                return Result<CreditCardPaymentConfirmationDto>.Failure("The card number entered does not correspond to a valid card.");
            }

            if (dto.Amount <= 0)
            {
                return Result<CreditCardPaymentConfirmationDto>.Failure("The payment amount must be greater than zero.");
            }

            if (card.CurrentDebt <= 0)
            {
                await LogRejectedCreditCardPaymentAsync(account, card, dto.Amount, cashierId);
                return Result<CreditCardPaymentConfirmationDto>.Failure("The selected card has no pending debt.");
            }

            var effectiveAmount = Math.Min(dto.Amount, card.CurrentDebt);

            if (account.Balance < effectiveAmount)
            {
                await LogRejectedCreditCardPaymentAsync(account, card, dto.Amount, cashierId);
                return Result<CreditCardPaymentConfirmationDto>.Failure("The amount entered exceeds the account's available balance.");
            }

            var accountHolder = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);
            var cardHolder = await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);

            return Result<CreditCardPaymentConfirmationDto>.Success(new CreditCardPaymentConfirmationDto
            {
                SourceAccountNumber = account.AccountNumber,
                AccountHolderName = accountHolder!.FullName,
                CardNumber = card.CardNumber,
                CardLastFourDigits = card.CardNumber[^4..],
                CardHolderName = cardHolder!.FullName,
                EnteredAmount = dto.Amount,
                EffectiveAmount = effectiveAmount
            });
        }

        public async Task<Result> ConfirmCreditCardPaymentAsync(CreditCardPaymentConfirmationDto dto, string cashierId)
        {
            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (account is null || account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The account number entered does not correspond to a valid account.");
            }

            var card = await _creditCardRepository.GetByCardNumberAsync(dto.CardNumber);
            if (card is null || card.Status != CreditCardStatus.Active)
            {
                return Result.Failure("The card number entered does not correspond to a valid card.");
            }

            if (dto.EnteredAmount <= 0)
            {
                return Result.Failure("The payment amount must be greater than zero.");
            }

            if (card.CurrentDebt <= 0)
            {
                await LogRejectedCreditCardPaymentAsync(account, card, dto.EnteredAmount, cashierId);
                return Result.Failure("The selected card has no pending debt.");
            }

            var effectiveAmount = Math.Min(dto.EnteredAmount, card.CurrentDebt);

            if (account.Balance < effectiveAmount)
            {
                await LogRejectedCreditCardPaymentAsync(account, card, dto.EnteredAmount, cashierId);
                return Result.Failure("The amount entered exceeds the account's available balance.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                account.Balance -= effectiveAmount;
                await _savingsAccountRepository.UpdateAsync(account);

                card.CurrentDebt -= effectiveAmount;
                await _creditCardRepository.UpdateAsync(card);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = account.Id,
                    Amount = effectiveAmount,
                    Type = TransactionType.Debit,
                    Category = TransactionCategory.CreditCardPayment,
                    Origin = account.AccountNumber,
                    Beneficiary = card.CardNumber[^4..],
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = cashierId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Credit card payment of {Amount:C} applied to card ending in {CardLastFour} from account ending in {AccountLastFour} by cashier {CashierId}.",
                effectiveAmount, card.CardNumber[^4..], account.AccountNumber[^4..], cashierId);

            var emailsSent = await TrySendCreditCardPaymentEmailsAsync(account, card, effectiveAmount);

            return Result.Success(emailsSent
                ? "The payment was completed successfully."
                : "The payment was completed successfully, but the notification email could not be sent.");
        }

        public async Task<Result<ThirdPartyTransactionConfirmationDto>> ValidateThirdPartyTransactionAsync(ThirdPartyTransactionDto dto, string cashierId)
        {
            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null)
            {
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The source account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The source account number entered does not correspond to a valid account.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The destination account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The source account and the destination account cannot be the same.");
            }

            if (dto.Amount <= 0)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The transaction amount must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result<ThirdPartyTransactionConfirmationDto>.Failure("The amount entered exceeds the account's available balance.");
            }

            var sourceClient = await _basicUserInfoService.GetBasicInfoAsync(sourceAccount.ClientId);
            var destinationClient = await _basicUserInfoService.GetBasicInfoAsync(destinationAccount.ClientId);

            return Result<ThirdPartyTransactionConfirmationDto>.Success(new ThirdPartyTransactionConfirmationDto
            {
                SourceAccountNumber = sourceAccount.AccountNumber,
                SourceAccountHolderName = sourceClient!.FullName,
                DestinationAccountNumber = destinationAccount.AccountNumber,
                DestinationAccountHolderName = destinationClient!.FullName,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmThirdPartyTransactionAsync(ThirdPartyTransactionConfirmationDto dto, string cashierId)
        {
            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null)
            {
                return Result.Failure("The source account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result.Failure("The source account number entered does not correspond to a valid account.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result.Failure("The destination account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result.Failure("The source account and the destination account cannot be the same.");
            }

            if (dto.Amount <= 0)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result.Failure("The transaction amount must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedThirdPartyTransactionAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, cashierId);
                return Result.Failure("The amount entered exceeds the account's available balance.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                sourceAccount.Balance -= dto.Amount;
                await _savingsAccountRepository.UpdateAsync(sourceAccount);

                destinationAccount.Balance += dto.Amount;
                await _savingsAccountRepository.UpdateAsync(destinationAccount);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = sourceAccount.Id,
                    Amount = dto.Amount,
                    Type = TransactionType.Debit,
                    Category = TransactionCategory.Transfer,
                    Origin = sourceAccount.AccountNumber,
                    Beneficiary = destinationAccount.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = cashierId,
                    CreatedAt = DateTime.UtcNow
                });

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = destinationAccount.Id,
                    Amount = dto.Amount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.Transfer,
                    Origin = sourceAccount.AccountNumber,
                    Beneficiary = destinationAccount.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = cashierId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation(
                "Transfer of {Amount:C} from account ending in {SourceLastFour} to account ending in {DestinationLastFour} by cashier {CashierId}.",
                dto.Amount, sourceAccount.AccountNumber[^4..], destinationAccount.AccountNumber[^4..], cashierId);

            var emailsSent = await TrySendThirdPartyTransactionEmailsAsync(sourceAccount, destinationAccount, dto.Amount);

            return Result.Success(emailsSent
                ? "The transaction was completed successfully."
                : "The transaction was completed successfully, but one or more notification emails could not be sent.");
        }

        #region Private Methods

        private async Task<bool> TrySendDepositEmailAsync(SavingsAccount account, decimal amount)
        {
            var lastFour = account.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);

                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = $"Deposit made to your account {lastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A deposit has been made to your account ending in <strong>{lastFour}</strong>.</p>
                        <p>Amount deposited: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Deposit email for account ending in {LastFourDigits} was not sent: {Error}", lastFour, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send deposit email for account ending in {LastFourDigits}.", lastFour);
                return false;
            }
        }

        private async Task LogRejectedWithdrawalAsync(SavingsAccount account, decimal amount, string cashierId)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = amount,
                Type = TransactionType.Debit,
                Category = TransactionCategory.Withdrawal,
                Origin = account.AccountNumber,
                Beneficiary = "WITHDRAWAL",
                Status = TransactionStatus.Rejected,
                PerformedByUserId = cashierId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Withdrawal of {Amount:C} from account ending in {LastFourDigits} was rejected due to insufficient funds. Cashier: {CashierId}.", amount, account.AccountNumber[^4..], cashierId);
        }

        private async Task<bool> TrySendWithdrawalEmailAsync(SavingsAccount account, decimal amount)
        {
            var lastFour = account.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);

                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = $"Withdrawal made from your account {lastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A withdrawal has been made from your account ending in <strong>{lastFour}</strong>.</p>
                        <p>Amount withdrawn: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Withdrawal email for account ending in {LastFourDigits} was not sent: {Error}", lastFour, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send withdrawal email for account ending in {LastFourDigits}.", lastFour);
                return false;
            }
        }

        private async Task LogRejectedCreditCardPaymentAsync(SavingsAccount account, CreditCard card, decimal enteredAmount, string cashierId)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = enteredAmount,
                Type = TransactionType.Debit,
                Category = TransactionCategory.CreditCardPayment,
                Origin = account.AccountNumber,
                Beneficiary = card.CardNumber[^4..],
                Status = TransactionStatus.Rejected,
                PerformedByUserId = cashierId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning(
                "Credit card payment attempt of {Amount:C} from account ending in {AccountLastFour} to card ending in {CardLastFour} was rejected. Cashier: {CashierId}.",
                enteredAmount, account.AccountNumber[^4..], card.CardNumber[^4..], cashierId);
        }

        private async Task<bool> TrySendCreditCardPaymentEmailsAsync(SavingsAccount account, CreditCard card, decimal amount)
        {
            var accountHolder = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);
            var cardHolder = account.ClientId == card.ClientId
                ? accountHolder
                : await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);

            var cardLastFour = card.CardNumber[^4..];
            var accountLastFour = account.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            var cardHolderEmailSent = await TrySendEmailAsync(
                cardHolder!.Email,
                $"Payment made to card {cardLastFour}",
                $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{cardHolder.FullName}</span></h3>
                    <p>A payment has been made to your credit card ending in <strong>{cardLastFour}</strong>.</p>
                    <p>Amount paid: <strong>RD$ {amount:N2}</strong></p>
                    <p>Source account ending in: <strong>{accountLastFour}</strong></p>
                    <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                    <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                """,
                $"card owner, card ending in {cardLastFour}");

            if (account.ClientId == card.ClientId)
            {
                return cardHolderEmailSent;
            }

            var accountHolderEmailSent = await TrySendEmailAsync(
                accountHolder!.Email,
                $"Payment made using your account {accountLastFour}",
                $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{accountHolder.FullName}</span></h3>
                    <p>A credit card payment was made using your account ending in <strong>{accountLastFour}</strong>.</p>
                    <p>Amount debited: <strong>RD$ {amount:N2}</strong></p>
                    <p>Card paid, ending in: <strong>{cardLastFour}</strong></p>
                    <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                    <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                """,
                $"account owner, account ending in {accountLastFour}");

            return cardHolderEmailSent && accountHolderEmailSent;
        }

        private async Task<bool> TrySendEmailAsync(string to, string subject, string bodyHtml, string context)
        {
            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto { To = to, Subject = subject, BodyHtml = bodyHtml });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Credit card payment email ({Context}) was not sent: {Error}", context, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send credit card payment email ({Context}).", context);
                return false;
            }
        }

        private async Task LogRejectedThirdPartyTransactionAsync(SavingsAccount sourceAccount, string destinationAccountNumber, decimal amount, string cashierId)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Id = 0,
                SavingsAccountId = sourceAccount.Id,
                Amount = amount,
                Type = TransactionType.Debit,
                Category = TransactionCategory.Transfer,
                Origin = sourceAccount.AccountNumber,
                Beneficiary = destinationAccountNumber,
                Status = TransactionStatus.Rejected,
                PerformedByUserId = cashierId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Third-party transaction attempt of {Amount:C} from account ending in {SourceLastFour} to account {DestinationAccountNumber} was rejected. Cashier: {CashierId}.",
                amount, sourceAccount.AccountNumber[^4..], destinationAccountNumber, cashierId);
        }

        private async Task<bool> TrySendThirdPartyTransactionEmailsAsync(SavingsAccount sourceAccount, SavingsAccount destinationAccount, decimal amount)
        {
            var sourceClient = await _basicUserInfoService.GetBasicInfoAsync(sourceAccount.ClientId);
            var destinationClient = await _basicUserInfoService.GetBasicInfoAsync(destinationAccount.ClientId);

            var sourceLastFour = sourceAccount.AccountNumber[^4..];
            var destinationLastFour = destinationAccount.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            var sourceEmailSent = await TrySendEmailAsync(
                sourceClient!.Email,
                $"Transaction made to account {destinationLastFour}",
                $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{sourceClient.FullName}</span></h3>
                    <p>A transaction has been made from your account ending in <strong>{sourceLastFour}</strong> to account ending in <strong>{destinationLastFour}</strong>.</p>
                    <p>Amount transferred: <strong>RD$ {amount:N2}</strong></p>
                    <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                    <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                """,
                $"source account owner, account ending in {sourceLastFour}");

            var destinationEmailSent = await TrySendEmailAsync(
                destinationClient!.Email,
                $"Transaction sent from account {sourceLastFour}",
                $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{destinationClient.FullName}</span></h3>
                    <p>You have received a transaction from account ending in <strong>{sourceLastFour}</strong> to your account ending in <strong>{destinationLastFour}</strong>.</p>
                    <p>Amount received: <strong>RD$ {amount:N2}</strong></p>
                    <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                    <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                """,
                $"destination account owner, account ending in {destinationLastFour}");

            return sourceEmailSent && destinationEmailSent;
        }

        #endregion
    }
}
