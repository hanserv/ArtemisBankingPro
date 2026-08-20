using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Assign
{
    /// <summary>
    /// Parameters required to assign a new credit card to a client.
    /// </summary>
    public class AssignCreditCardCommand : IRequest<CreditCardDto>
    {
        /// <example>20</example>
        [SwaggerParameter(Description = "Identifier of the client the card will be assigned to.")]
        public required string ClientId { get; set; }

        /// <example>50000.00</example>
        [SwaggerParameter(Description = "Approved credit limit for the card.")]
        public required decimal CreditLimit { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class AssignCreditCardCommandHandler : IRequestHandler<AssignCreditCardCommand, CreditCardDto>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly ICardNumberGenerator _cardNumberGenerator;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AssignCreditCardCommandHandler> _logger;
        private readonly IMapper _mapper;

        public AssignCreditCardCommandHandler(ICreditCardRepository creditCardRepository, ICardNumberGenerator cardNumberGenerator,
            IBasicUserInfoService basicUserInfoService, IEmailService emailService,
            ILogger<AssignCreditCardCommandHandler> logger, IMapper mapper)
        {
            _creditCardRepository = creditCardRepository;
            _cardNumberGenerator = cardNumberGenerator;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<CreditCardDto> Handle(AssignCreditCardCommand request, CancellationToken cancellationToken)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(request.ClientId);
            if (client is null)
            {
                throw new ApiException("The specified client does not exist.", (int)HttpStatusCode.NotFound);
            }

            string cardNumber;
            try
            {
                cardNumber = await _cardNumberGenerator.GenerateAsync();
            }
            catch (InvalidOperationException)
            {
                throw new ApiException("It was not possible to generate a unique card number. Please try again.", (int)HttpStatusCode.Conflict);
            }

            var cvc = NumericStringGenerator.Generate(3);
            var expirationDate = DateTime.UtcNow.AddYears(3).ToString("MM/yy");

            var card = new Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = cardNumber,
                ClientId = request.ClientId,
                CreditLimit = request.CreditLimit,
                CurrentDebt = 0m,
                ExpirationDate = expirationDate,
                CvcHash = Sha256Helper.Hash(cvc),
                CreatedByAdminId = request.AdminId,
                Status = CreditCardStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            await _creditCardRepository.AddAsync(card);

            _logger.LogInformation("Credit card ending in {LastFourDigits} assigned to client {ClientId} by administrator {AdminId} with a credit limit of {CreditLimit:C}.",
                card.CardNumber[^4..], request.ClientId, request.AdminId, request.CreditLimit);

            var emailSent = await TrySendAssignmentEmailAsync(card, client);

            if (!emailSent)
            {
                _logger.LogWarning("Credit card ending in {LastFourDigits} was created for client {ClientId}, but the assignment notification email could not be sent.",
                    card.CardNumber[^4..], request.ClientId);
            }

            var responseDto = _mapper.Map<CreditCardDto>(card);
            responseDto.ClientFullName = client.FullName;
            responseDto.CreatedByAdminName = string.Empty;

            return responseDto;
        }


        #region Private Methods

        private async Task<bool> TrySendAssignmentEmailAsync(Domain.Entities.CreditCard card, UserBasicInfoDto client)
        {
            var lastFour = card.CardNumber[^4..];

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client.Email,
                    Subject = "New credit card assigned",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>A new credit card has been assigned to your account.</p>
                        <p>Card ending in: <strong>{lastFour}</strong></p>
                        <p>Approved limit: <strong>RD$ {card.CreditLimit:N2}</strong></p>
                        <p>Expiration date: <strong>{card.ExpirationDate}</strong></p>
                        <p>Assignment date: <strong>{card.CreatedAt:dd/MM/yyyy}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">For your security, do not share your card information with anyone.</p>
                    """
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send assignment notification email for credit card ending in {LastFourDigits} to client {ClientId}.", lastFour, card.ClientId);
                return false;
            }
        }

        #endregion
    }
}
