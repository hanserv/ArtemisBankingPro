using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.HermesPay.Commands.ProcessPayment
{
    /// <summary>
    /// Parameters required to process a credit card payment in favor of a commerce.
    /// </summary>
    public class ProcessCommercePaymentCommand : IRequest
    {
        [BindNever]
        public int CommerceId { get; set; }
        [BindNever]
        public string PerformedByUserId { get; set; } = string.Empty;

        /// <example>1589963258467598</example>
        [SwaggerParameter(Description = "16-digit credit card number.")]
        public required string CardNumber { get; set; }

        /// <example>02</example>
        [SwaggerParameter(Description = "Card expiration month, in MM format.")]
        public required string MonthExpirationCard { get; set; }

        /// <example>2028</example>
        [SwaggerParameter(Description = "Card expiration year, in YYYY format.")]
        public required string YearExpirationCard { get; set; }

        /// <example>859</example>
        [SwaggerParameter(Description = "3-digit card security code.")]
        public required string Cvc { get; set; }

        /// <example>689.25</example>
        [SwaggerParameter(Description = "Amount to process as a payment to the commerce.")]
        public required decimal TransactionAmount { get; set; }
    }

    public class ProcessCommercePaymentCommandHandler : IRequestHandler<ProcessCommercePaymentCommand>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly ICommerceRepository _commerceRepository;
        private readonly ICardConsumptionRepository _cardConsumptionRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProcessCommercePaymentCommandHandler> _logger;

        public ProcessCommercePaymentCommandHandler(ICreditCardRepository creditCardRepository, ICommerceRepository commerceRepository,
            ICardConsumptionRepository cardConsumptionRepository, ISavingsAccountRepository savingsAccountRepository,
            ITransactionRepository transactionRepository, IBasicUserInfoService basicUserInfoService,
            IEmailService emailService, IUnitOfWork unitOfWork,
            ILogger<ProcessCommercePaymentCommandHandler> logger)
        {
            _creditCardRepository = creditCardRepository;
            _commerceRepository = commerceRepository;
            _cardConsumptionRepository = cardConsumptionRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Handle(ProcessCommercePaymentCommand request, CancellationToken cancellationToken)
        {
            var commerce = await _commerceRepository.GetByIdAsync(request.CommerceId);
            if (commerce is null)
            {
                throw new ApiException("The specified commerce does not exist.", (int)HttpStatusCode.NotFound);
            }

            var card = await _creditCardRepository.GetByCardNumberAsync(request.CardNumber);
            if (card is null)
            {
                throw new ApiException("The card does not exist.", (int)HttpStatusCode.BadRequest);
            }

            var commerceUserId = await _basicUserInfoService.GetUserIdByCommerceIdAsync(commerce.Id);
            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(commerceUserId!);

            var availableCredit = card.CreditLimit - card.CurrentDebt;

            if (request.TransactionAmount > availableCredit)
            {
                await _cardConsumptionRepository.AddAsync(new CardConsumption
                {
                    Id = 0,
                    CreditCardId = card.Id,
                    CommerceId = commerce.Id,
                    Amount = request.TransactionAmount,
                    Status = ConsumptionStatus.Rejected,
                    ConsumptionDate = DateTime.UtcNow
                });

                _logger.LogWarning(
                    "Hermes Pay payment attempt of {Amount:C} to commerce {CommerceName} on card ending in {CardLastFour} was rejected due to insufficient available credit.",
                    request.TransactionAmount, commerce.Name, card.CardNumber[^4..]);

                throw new ApiException("The transaction amount exceeds the available credit of the card.", (int)HttpStatusCode.BadRequest);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                card.CurrentDebt += request.TransactionAmount;
                await _creditCardRepository.UpdateAsync(card);

                await _cardConsumptionRepository.AddAsync(new CardConsumption
                {
                    Id = 0,
                    CreditCardId = card.Id,
                    CommerceId = commerce.Id,
                    Amount = request.TransactionAmount,
                    Status = ConsumptionStatus.Approved,
                    ConsumptionDate = DateTime.UtcNow
                });

                principalAccount!.Balance += request.TransactionAmount;
                await _savingsAccountRepository.UpdateAsync(principalAccount);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = principalAccount.Id,
                    Amount = request.TransactionAmount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.HermesPayment,
                    Origin = card.CardNumber[^4..],
                    Beneficiary = principalAccount.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = request.PerformedByUserId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Hermes Pay payment of {Amount:C} processed to commerce {CommerceName} on card ending in {CardLastFour}.",
                request.TransactionAmount, commerce.Name, card.CardNumber[^4..]);

            await TrySendCardHolderEmailAsync(card, commerce, request.TransactionAmount);
            await TrySendCommerceEmailAsync(commerce, card, request.TransactionAmount);
        }

        #region Private Methods
        private async Task TrySendCardHolderEmailAsync(Domain.Entities.CreditCard card, Domain.Entities.Commerce commerce, decimal amount)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);
            var lastFour = card.CardNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = $"Consumption made with card {lastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A consumption has been made with your card ending in <strong>{lastFour}</strong>.</p>
                        <p>Commerce: <strong>{commerce.Name}</strong></p>
                        <p>Amount: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send consumption notification email for card ending in {LastFourDigits} to client {ClientId}.", lastFour, card.ClientId);
            }
        }

        private async Task TrySendCommerceEmailAsync(Domain.Entities.Commerce commerce, Domain.Entities.CreditCard card, decimal amount)
        {
            var lastFour = card.CardNumber[^4..];
            var performedAt = DateTime.UtcNow;

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = commerce.Email,
                    Subject = $"Payment received through card {lastFour}",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{commerce.Name}</span></h3>
                        <p>You have received a new payment through Hermes Pay.</p>
                        <p>Card ending in: <strong>{lastFour}</strong></p>
                        <p>Amount received: <strong>RD$ {amount:N2}</strong></p>
                        <p>Date and time: <strong>{performedAt:MM/dd/yyyy hh:mm tt}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">This message serves as proof of the payment received.</p>
                    """
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment notification email to commerce {CommerceName} (id {CommerceId}).", commerce.Name, commerce.Id);
            }
        }

        #endregion
    }
}
