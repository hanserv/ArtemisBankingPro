using System.Net;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.CancelSecondary
{
    public class CancelSecondaryAccountCommand : IRequest
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string PerformedByAdminId { get; set; } = string.Empty;
    }

    public class CancelSecondaryAccountCommandHandler : IRequestHandler<CancelSecondaryAccountCommand>
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CancelSecondaryAccountCommandHandler> _logger;

        public CancelSecondaryAccountCommandHandler(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IUnitOfWork unitOfWork, ILogger<CancelSecondaryAccountCommandHandler> logger)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Handle(CancelSecondaryAccountCommand request, CancellationToken cancellationToken)
        {
            var account = await _savingsAccountRepository.GetByAccountNumberAsync(request.AccountNumber);
            if (account is null)
            {
                throw new ApiException("The selected account does not exist.", (int)HttpStatusCode.NotFound);
            }

            if (account.Status != SavingsAccountStatus.Active)
            {
                throw new ApiException("The selected account is already cancelled.", (int)HttpStatusCode.BadRequest);
            }

            if (account.Type != SavingsAccountType.Secondary)
            {
                throw new ApiException("Principal accounts cannot be cancelled.", (int)HttpStatusCode.BadRequest);
            }

            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(account.ClientId);
            if (principalAccount is null)
            {
                _logger.LogWarning("Cancellation of secondary account {AccountNumber} failed: client {ClientId} has no active principal account to receive the funds.", account.AccountNumber, account.ClientId);
                throw new ApiException("It is not possible to cancel the account because the client does not have an active principal account to receive the funds.", (int)HttpStatusCode.BadRequest);
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
                        PerformedByUserId = request.PerformedByAdminId,
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
                        PerformedByUserId = request.PerformedByAdminId,
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

            _logger.LogInformation("Secondary account {AccountNumber} cancelled by administrator {AdminId}.", account.AccountNumber, request.PerformedByAdminId);
        }
    }
}