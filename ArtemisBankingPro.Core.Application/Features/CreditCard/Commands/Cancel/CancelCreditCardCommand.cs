using System.Net;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Cancel
{
    /// <summary>
    /// Parameters required to cancel a credit card.
    /// </summary>
    public class CancelCreditCardCommand : IRequest<Unit>
    {
        public int CreditCardId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class CancelCreditCardCommandHandler : IRequestHandler<CancelCreditCardCommand, Unit>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly ILogger<CancelCreditCardCommandHandler> _logger;

        public CancelCreditCardCommandHandler(ICreditCardRepository creditCardRepository, ILogger<CancelCreditCardCommandHandler> logger)
        {
            _creditCardRepository = creditCardRepository;
            _logger = logger;
        }

        public async Task<Unit> Handle(CancelCreditCardCommand request, CancellationToken cancellationToken)
        {
            var card = await _creditCardRepository.GetByIdAsync(request.CreditCardId);
            if (card is null)
            {
                throw new ApiException("The selected credit card does not exist.", (int)HttpStatusCode.NotFound);
            }

            card.Status = CreditCardStatus.Cancelled;
            await _creditCardRepository.UpdateAsync(card);

            _logger.LogInformation("Credit card ending in {LastFourDigits} cancelled by administrator {AdminId}.",
                card.CardNumber[^4..], request.AdminId);

            return Unit.Value;
        }
    }
}
