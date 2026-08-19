using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class ClientTransactionService : IClientTransactionService
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ClientTransactionService> _logger;
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly ICardConsumptionRepository _cardConsumptionRepository;

        public ClientTransactionService(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IBasicUserInfoService basicUserInfoService, IEmailService emailService,
            IUnitOfWork unitOfWork, ILogger<ClientTransactionService> logger,
            ICreditCardRepository creditCardRepository, IBeneficiaryRepository beneficiaryRepository,
            ICardConsumptionRepository cardConsumptionRepository)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _creditCardRepository = creditCardRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _cardConsumptionRepository = cardConsumptionRepository;
        }

        public async Task<Result<ExpressTransactionConfirmationDto>> ValidateExpressTransactionAsync(ExpressTransactionDto dto, string clientId)
        {
            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId || sourceAccount.Status != SavingsAccountStatus.Active)
            {
                return Result<ExpressTransactionConfirmationDto>.Failure("The selected source account is not valid.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                return Result<ExpressTransactionConfirmationDto>.Failure("The account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                return Result<ExpressTransactionConfirmationDto>.Failure("The destination account cannot be the same as the source account.");
            }

            if (dto.Amount <= 0)
            {
                return Result<ExpressTransactionConfirmationDto>.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId, TransactionCategory.Transfer);
                return Result<ExpressTransactionConfirmationDto>.Failure("The amount entered exceeds the available balance of the selected account.");
            }

            var destinationHolder = await _basicUserInfoService.GetBasicInfoAsync(destinationAccount.ClientId);

            return Result<ExpressTransactionConfirmationDto>.Success(new ExpressTransactionConfirmationDto
            {
                SourceAccountNumber = sourceAccount.AccountNumber,
                DestinationAccountNumber = destinationAccount.AccountNumber,
                DestinationAccountHolderName = destinationHolder!.FullName,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmExpressTransactionAsync(ExpressTransactionConfirmationDto dto, string clientId)
        {
            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId || sourceAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The selected source account is not valid.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The account number entered does not correspond to a valid account.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                return Result.Failure("The destination account cannot be the same as the source account.");
            }

            if (dto.Amount <= 0)
            {
                return Result.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId, TransactionCategory.Transfer);
                return Result.Failure("The amount entered exceeds the available balance of the selected account.");
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
                    PerformedByUserId = clientId,
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
                    PerformedByUserId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Express transaction of {Amount:C} from account ending in {SourceLastFour} to account ending in {DestinationLastFour} performed by client {ClientId}.",
                dto.Amount, sourceAccount.AccountNumber[^4..], destinationAccount.AccountNumber[^4..], clientId);

            var emailsSent = await TrySendTransferEmailsAsync(sourceAccount, destinationAccount, dto.Amount);

            return Result.Success(emailsSent
                ? "The transaction was completed successfully."
                : "The transaction was completed successfully, but one or more notification emails could not be sent.");
        }

        public async Task<Result> PayCreditCardAsync(ClientCreditCardPaymentDto dto, string clientId)
        {
            var card = await _creditCardRepository.GetByIdAsync(dto.CreditCardId);
            if (card is null || card.ClientId != clientId || card.Status != CreditCardStatus.Active)
            {
                return Result.Failure("The selected credit card is not valid.");
            }

            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId || sourceAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The selected source account is not valid.");
            }

            if (dto.Amount <= 0)
            {
                return Result.Failure("The payment amount must be greater than zero.");
            }

            if (card.CurrentDebt <= 0)
            {
                return Result.Failure("The selected card has no pending debt.");
            }

            var effectiveAmount = Math.Min(dto.Amount, card.CurrentDebt);

            if (sourceAccount.Balance < effectiveAmount)
            {
                await LogRejectedCreditCardPaymentAsync(sourceAccount, card, dto.Amount, clientId);
                return Result.Failure("You do not have the required amount in the selected account.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                sourceAccount.Balance -= effectiveAmount;
                await _savingsAccountRepository.UpdateAsync(sourceAccount);

                card.CurrentDebt -= effectiveAmount;
                await _creditCardRepository.UpdateAsync(card);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = sourceAccount.Id,
                    Amount = effectiveAmount,
                    Type = TransactionType.Debit,
                    Category = TransactionCategory.CreditCardPayment,
                    Origin = sourceAccount.AccountNumber,
                    Beneficiary = card.CardNumber[^4..],
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Credit card payment of {Amount:C} applied to card ending in {CardLastFour} from account ending in {AccountLastFour} by client {ClientId}.",
                effectiveAmount, card.CardNumber[^4..], sourceAccount.AccountNumber[^4..], clientId);

            var emailSent = await TrySendCreditCardPaymentEmailAsync(sourceAccount, card, effectiveAmount);

            return Result.Success(emailSent
                ? "The payment was completed successfully."
                : "The payment was completed successfully, but the notification email could not be sent.");
        }

        public async Task<Result<BeneficiaryTransactionConfirmationDto>> ValidateBeneficiaryTransactionAsync(BeneficiaryTransactionDto dto, string clientId)
        {
            var beneficiary = await _beneficiaryRepository.GetAllQueryInclude(["SavingsAccount"])
                    .FirstOrDefaultAsync(b => b.Id == dto.BeneficiaryId && b.ClientId == clientId);

            if (beneficiary is null)
            {
                return Result<BeneficiaryTransactionConfirmationDto>.Failure("The selected beneficiary is not valid.");
            }

            if (beneficiary.SavingsAccount is null || beneficiary.SavingsAccount.Status != SavingsAccountStatus.Active)
            {
                return Result<BeneficiaryTransactionConfirmationDto>.Failure("The beneficiary account is not available.");
            }

            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId || sourceAccount.Status != SavingsAccountStatus.Active)
            {
                return Result<BeneficiaryTransactionConfirmationDto>.Failure("The selected source account is not valid.");
            }

            if (dto.Amount <= 0)
            {
                return Result<BeneficiaryTransactionConfirmationDto>.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedTransferAsync(sourceAccount, beneficiary.SavingsAccount.AccountNumber, dto.Amount, clientId, TransactionCategory.Transfer);
                return Result<BeneficiaryTransactionConfirmationDto>.Failure("You do not have sufficient funds to complete this transaction.");
            }

            var destinationHolder = await _basicUserInfoService.GetBasicInfoAsync(beneficiary.SavingsAccount.ClientId);

            return Result<BeneficiaryTransactionConfirmationDto>.Success(new BeneficiaryTransactionConfirmationDto
            {
                SourceAccountNumber = sourceAccount.AccountNumber,
                DestinationAccountNumber = beneficiary.SavingsAccount.AccountNumber,
                DestinationAccountHolderName = destinationHolder!.FullName,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmBeneficiaryTransactionAsync(BeneficiaryTransactionConfirmationDto dto, string clientId)
        {
            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId || sourceAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The selected source account is not valid.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The beneficiary account is not available.");
            }

            if (dto.Amount <= 0)
            {
                return Result.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedTransferAsync(sourceAccount, destinationAccount.AccountNumber, dto.Amount, clientId, TransactionCategory.Transfer);
                return Result.Failure("You do not have sufficient funds to complete this transaction.");
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
                    PerformedByUserId = clientId,
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
                    PerformedByUserId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Beneficiary transaction of {Amount:C} from account ending in {SourceLastFour} to beneficiary account ending in {DestinationLastFour} performed by client {ClientId}.",
                dto.Amount, sourceAccount.AccountNumber[^4..], destinationAccount.AccountNumber[^4..], clientId);

            var emailsSent = await TrySendTransferEmailsAsync(sourceAccount, destinationAccount, dto.Amount);

            return Result.Success(emailsSent
                ? "The transaction was completed successfully."
                : "The transaction was completed successfully, but one or more notification emails could not be sent.");
        }

        public async Task<Result<OwnAccountTransferConfirmationDto>> ValidateOwnAccountTransferAsync(OwnAccountTransferDto dto, string clientId)
        {
            var hasEnoughAccounts = await HasAtLeastTwoActiveAccountsAsync(clientId);
            if (!hasEnoughAccounts)
            {
                return Result<OwnAccountTransferConfirmationDto>.Failure(
                    "You must have at least two active savings accounts to make a transfer between accounts.");
            }

            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId)
            {
                return Result<OwnAccountTransferConfirmationDto>.Failure("The selected source account is not valid.");
            }

            if (sourceAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result<OwnAccountTransferConfirmationDto>.Failure("The selected source account is not valid.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.ClientId != clientId || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result<OwnAccountTransferConfirmationDto>.Failure("The selected destination account is not valid.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result<OwnAccountTransferConfirmationDto>.Failure("The source account and the destination account cannot be the same.");
            }

            if (dto.Amount <= 0)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result<OwnAccountTransferConfirmationDto>.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result<OwnAccountTransferConfirmationDto>.Failure("You do not have the required amount in the selected account.");
            }

            return Result<OwnAccountTransferConfirmationDto>.Success(new OwnAccountTransferConfirmationDto
            {
                SourceAccountNumber = sourceAccount.AccountNumber,
                DestinationAccountNumber = destinationAccount.AccountNumber,
                Amount = dto.Amount
            });
        }

        public async Task<Result> ConfirmOwnAccountTransferAsync(OwnAccountTransferConfirmationDto dto, string clientId)
        {
            var hasEnoughAccounts = await HasAtLeastTwoActiveAccountsAsync(clientId);
            if (!hasEnoughAccounts)
            {
                return Result.Failure("You must have at least two active savings accounts to make a transfer between accounts.");
            }

            var sourceAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.SourceAccountNumber);
            if (sourceAccount is null || sourceAccount.ClientId != clientId)
            {
                return Result.Failure("The selected source account is not valid.");
            }

            if (sourceAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result.Failure("The selected source account is not valid.");
            }

            var destinationAccount = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (destinationAccount is null || destinationAccount.ClientId != clientId || destinationAccount.Status != SavingsAccountStatus.Active)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result.Failure("The selected destination account is not valid.");
            }

            if (sourceAccount.AccountNumber == destinationAccount.AccountNumber)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result.Failure("The source account and the destination account cannot be the same.");
            }

            if (dto.Amount <= 0)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result.Failure("The amount to transfer must be greater than zero.");
            }

            if (sourceAccount.Balance < dto.Amount)
            {
                await LogRejectedOwnAccountTransferAsync(sourceAccount, dto.DestinationAccountNumber, dto.Amount, clientId);
                return Result.Failure("You do not have the required amount in the selected account.");
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
                    PerformedByUserId = clientId,
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
                    PerformedByUserId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Own-account transfer of {Amount:C} from account ending in {SourceLastFour} to account ending in {DestinationLastFour} performed by client {ClientId}.",
                dto.Amount, sourceAccount.AccountNumber[^4..], destinationAccount.AccountNumber[^4..], clientId);

            var emailSent = await TrySendOwnAccountTransferEmailAsync(sourceAccount, destinationAccount, dto.Amount);

            return Result.Success(emailSent
                ? "The transfer was completed successfully."
                : "The transfer was completed successfully, but the notification email could not be sent.");
        }

        public async Task<Result> RequestCashAdvanceAsync(CashAdvanceDto dto, string clientId)
        {
            var card = await _creditCardRepository.GetByIdAsync(dto.CreditCardId);
            if (card is null || card.ClientId != clientId)
            {
                return Result.Failure("The selected credit card is not valid.");
            }

            if (card.Status != CreditCardStatus.Active)
            {
                return Result.Failure("The selected card is not active.");
            }

            if (IsCardExpired(card.ExpirationDate))
            {
                return Result.Failure("The selected card is expired.");
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(dto.DestinationAccountNumber);
            if (account is null || account.ClientId != clientId || account.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure("The selected savings account is not active.");
            }

            if (dto.Amount <= 0)
            {
                return Result.Failure("The advance amount must be greater than zero.");
            }

            const decimal InterestRate = 0.0625m;
            var interest = Math.Round(dto.Amount * InterestRate, 2, MidpointRounding.AwayFromZero);
            var totalToCharge = dto.Amount + interest;
            var availableCredit = card.CreditLimit - card.CurrentDebt;

            if (totalToCharge > availableCredit)
            {
                await _cardConsumptionRepository.AddAsync(new CardConsumption
                {
                    Id = 0,
                    CreditCardId = card.Id,
                    CommerceId = null,
                    Amount = totalToCharge,
                    Status = ConsumptionStatus.Rejected,
                    ConsumptionDate = DateTime.UtcNow
                });

                _logger.LogWarning("Cash advance attempt of {Amount:C} (total {Total:C} with interest) on card ending in {CardLastFour} was rejected due to insufficient available credit. Client: {ClientId}.",
                    dto.Amount, totalToCharge, card.CardNumber[^4..], clientId);

                return Result.Failure("The requested advance exceeds the available credit of the selected card.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                account.Balance += dto.Amount;
                await _savingsAccountRepository.UpdateAsync(account);

                card.CurrentDebt += totalToCharge;
                await _creditCardRepository.UpdateAsync(card);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = account.Id,
                    Amount = dto.Amount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.CashAdvance,
                    Origin = card.CardNumber[^4..],
                    Beneficiary = account.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = clientId,
                    CreatedAt = DateTime.UtcNow
                });

                await _cardConsumptionRepository.AddAsync(new CardConsumption
                {
                    Id = 0,
                    CreditCardId = card.Id,
                    CommerceId = null,
                    Amount = totalToCharge,
                    Status = ConsumptionStatus.Approved,
                    ConsumptionDate = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Cash advance of {Amount:C} (total charged {Total:C}) processed on card ending in {CardLastFour} to account ending in {AccountLastFour} by client {ClientId}.",
                dto.Amount, totalToCharge, card.CardNumber[^4..], account.AccountNumber[^4..], clientId);

            var emailSent = await TrySendCashAdvanceEmailAsync(account, card, dto.Amount, interest, totalToCharge);

            return Result.Success(emailSent
                ? "The cash advance was completed successfully."
                : "The advance was completed successfully, but the notification email could not be sent.");
        }

        #region Private Methods

        private async Task LogRejectedTransferAsync(SavingsAccount sourceAccount, string destinationAccountNumber, decimal amount, string clientId, TransactionCategory category)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Id = 0,
                SavingsAccountId = sourceAccount.Id,
                Amount = amount,
                Type = TransactionType.Debit,
                Category = category,
                Origin = sourceAccount.AccountNumber,
                Beneficiary = destinationAccountNumber,
                Status = TransactionStatus.Rejected,
                PerformedByUserId = clientId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Transfer attempt of {Amount:C} from account ending in {SourceLastFour} to account {DestinationAccountNumber} was rejected due to insufficient funds. Client: {ClientId}.",
                amount, sourceAccount.AccountNumber[^4..], destinationAccountNumber, clientId);
        }

        private async Task<bool> TrySendTransferEmailsAsync(SavingsAccount sourceAccount, SavingsAccount destinationAccount, decimal amount)
        {
            var sourceClient = await _basicUserInfoService.GetBasicInfoAsync(sourceAccount.ClientId);
            var destinationClient = await _basicUserInfoService.GetBasicInfoAsync(destinationAccount.ClientId);

            var sourceLastFour = sourceAccount.AccountNumber[^4..];
            var destinationLastFour = destinationAccount.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            var senderEmailSent = await TrySendEmailAsync(sourceClient!.Email,
                $"Transaction made to account {destinationLastFour}",
                $"""
                    <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{sourceClient.FullName}</span></h3>
                    <p>You have made a transaction from your account ending in <strong>{sourceLastFour}</strong> to account ending in <strong>{destinationLastFour}</strong>.</p>
                    <p>Amount transferred: <strong>RD$ {amount:N2}</strong></p>
                    <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                    <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                """,
                $"sender, account ending in {sourceLastFour}");

                var receiverEmailSent = await TrySendEmailAsync(destinationClient!.Email,
                    $"Transaction sent from account {sourceLastFour}",
                    $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{destinationClient.FullName}</span></h3>
                        <p>You have received a transaction from account ending in <strong>{sourceLastFour}</strong> to your account ending in <strong>{destinationLastFour}</strong>.</p>
                        <p>Amount received: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """,
                    $"receiver, account ending in {destinationLastFour}");

            return senderEmailSent && receiverEmailSent;
        }

        private async Task<bool> TrySendEmailAsync(string to, string subject, string bodyHtml, string context)
        {
            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto { To = to, Subject = subject, BodyHtml = bodyHtml });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Express transaction email ({Context}) was not sent: {Error}", context, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send express transaction email ({Context}).", context);
                return false;
            }
        }

        private async Task LogRejectedCreditCardPaymentAsync(SavingsAccount account, CreditCard card, decimal enteredAmount, string clientId)
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
                PerformedByUserId = clientId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Credit card payment attempt of {Amount:C} from account ending in {AccountLastFour} to card ending in {CardLastFour} was rejected due to insufficient funds. Client: {ClientId}.",
                enteredAmount, account.AccountNumber[^4..], card.CardNumber[^4..], clientId);
        }

        private async Task<bool> TrySendCreditCardPaymentEmailAsync(SavingsAccount account, CreditCard card, decimal amount)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);
            var cardLastFour = card.CardNumber[^4..];
            var accountLastFour = account.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = $"Payment made to card {cardLastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A payment has been made to your credit card ending in <strong>{cardLastFour}</strong>.</p>
                        <p>Amount paid: <strong>RD$ {amount:N2}</strong></p>
                        <p>Source account ending in: <strong>{accountLastFour}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Credit card payment email for card ending in {LastFourDigits} was not sent: {Error}", cardLastFour, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send credit card payment email for card ending in {LastFourDigits}.", cardLastFour);
                return false;
            }
        }

        private async Task<bool> HasAtLeastTwoActiveAccountsAsync(string clientId)
        {
            var activeAccounts = await _savingsAccountRepository.GetActiveByClientIdAsync(clientId);
            return activeAccounts.Count >= 2;
        }

        private async Task LogRejectedOwnAccountTransferAsync(SavingsAccount sourceAccount, string destinationAccountNumber, decimal amount, string clientId)
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
                PerformedByUserId = clientId,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Own-account transfer attempt of {Amount:C} from account ending in {SourceLastFour} to account {DestinationAccountNumber} was rejected. Client: {ClientId}.",
                amount, sourceAccount.AccountNumber[^4..], destinationAccountNumber, clientId);
        }

        private async Task<bool> TrySendOwnAccountTransferEmailAsync(SavingsAccount sourceAccount, SavingsAccount destinationAccount, decimal amount)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(sourceAccount.ClientId);
            var sourceLastFour = sourceAccount.AccountNumber[^4..];
            var destinationLastFour = destinationAccount.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = "Transfer between accounts completed",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A transfer has been made between your savings accounts.</p>
                        <p>Source account ending in: <strong>{sourceLastFour}</strong></p>
                        <p>Destination account ending in: <strong>{destinationLastFour}</strong></p>
                        <p>Amount transferred: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Own-account transfer email for account ending in {LastFourDigits} was not sent: {Error}", sourceLastFour, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send own-account transfer email for account ending in {LastFourDigits}.", sourceLastFour);
                return false;
            }
        }

        private static bool IsCardExpired(string expirationDate)
        {
            var parts = expirationDate.Split('/');
            var month = int.Parse(parts[0]);
            var year = 2000 + int.Parse(parts[1]);

            var lastDayOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            return DateTime.UtcNow.Date > lastDayOfMonth;
        }

        private async Task<bool> TrySendCashAdvanceEmailAsync(SavingsAccount account, CreditCard card, decimal amount, decimal interest, decimal totalCharged)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(account.ClientId);
            var cardLastFour = card.CardNumber[^4..];
            var accountLastFour = account.AccountNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = $"Cash advance from card {cardLastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A cash advance has been made from your card ending in <strong>{cardLastFour}</strong>.</p>
                        <p>Advance amount: <strong>RD$ {amount:N2}</strong></p>
                        <p>Interest applied: <strong>RD$ {interest:N2}</strong></p>
                        <p>Total charged to the card: <strong>RD$ {totalCharged:N2}</strong></p>
                        <p>Destination account ending in: <strong>{accountLastFour}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Cash advance email for card ending in {LastFourDigits} was not sent: {Error}", cardLastFour, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cash advance email for card ending in {LastFourDigits}.", cardLastFour);
                return false;
            }
        }

        #endregion
    }
}
