using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.ModifyLimit
{
    /// <summary>
    /// Parameters required to modify a credit card's credit limit.
    /// </summary>
    public class ModifyCreditCardLimitCommand : IRequest<Unit>
    {
        public int CreditCardId { get; set; }

        /// <example>75000.00</example>
        [SwaggerParameter(Description = "New approved credit limit for the card.")]
        public required decimal CreditLimit { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class ModifyCreditCardLimitCommandHandler : IRequestHandler<ModifyCreditCardLimitCommand, Unit>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ModifyCreditCardLimitCommandHandler> _logger;

        public ModifyCreditCardLimitCommandHandler(
            ICreditCardRepository creditCardRepository,
            IBasicUserInfoService basicUserInfoService,
            IEmailService emailService,
            ILogger<ModifyCreditCardLimitCommandHandler> logger)
        {
            _creditCardRepository = creditCardRepository;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Unit> Handle(ModifyCreditCardLimitCommand request, CancellationToken cancellationToken)
        {
            var card = await _creditCardRepository.GetByIdAsync(request.CreditCardId);
            if (card is null)
            {
                throw new ApiException("The selected credit card does not exist.", (int)HttpStatusCode.NotFound);
            }

            card.CreditLimit = request.CreditLimit;
            await _creditCardRepository.UpdateAsync(card);

            _logger.LogInformation("Credit limit for card ending in {LastFourDigits} updated to {NewLimit:C} by administrator {AdminId}.",
                card.CardNumber[^4..], request.CreditLimit, request.AdminId);

            var emailSent = await TrySendLimitChangeEmailAsync(card);

            if (!emailSent)
            {
                _logger.LogWarning("Credit limit for card ending in {LastFourDigits} was updated, but the notification email could not be sent.",
                    card.CardNumber[^4..]);
            }

            return Unit.Value;
        }

        #region Private Methods

        private async Task<bool> TrySendLimitChangeEmailAsync(Domain.Entities.CreditCard card)
        {
            var clientInfo = await _basicUserInfoService.GetBasicInfoAsync(card.ClientId);
            var lastFour = card.CardNumber[^4..];

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = clientInfo!.Email,
                    Subject = "Credit card limit modification",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{clientInfo.FullName}</span></h3>
                        <p>The credit limit of your credit card ending in <strong>{lastFour}</strong> has been updated.</p>
                        <p>New approved limit: <strong>RD$ {card.CreditLimit:N2}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this change, please contact the bank.</p>
                    """
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send limit change notification email for credit card ending in {LastFourDigits} to client {ClientId}.", lastFour, card.ClientId);
                return false;
            }
        }

        #endregion
    }
}
