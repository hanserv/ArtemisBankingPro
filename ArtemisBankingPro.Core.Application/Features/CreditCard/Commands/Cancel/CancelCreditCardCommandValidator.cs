using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Cancel
{
    public class CancelCreditCardCommandValidator : AbstractValidator<CancelCreditCardCommand>
    {
        private readonly ICreditCardRepository _creditCardRepository;

        public CancelCreditCardCommandValidator(ICreditCardRepository creditCardRepository)
        {
            _creditCardRepository = creditCardRepository;

            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellation) =>
                {
                    var card = await _creditCardRepository.GetByIdAsync(command.CreditCardId);

                    if (card is null)
                    {
                        return;
                    }

                    if (card.Status != CreditCardStatus.Active)
                    {
                        context.AddFailure(nameof(command.CreditCardId), "The selected credit card is already cancelled.");
                        return;
                    }

                    if (card.CurrentDebt > 0)
                    {
                        context.AddFailure(nameof(command.CreditCardId), "To cancel this card, the client must first settle the outstanding debt.");
                    }
                });
        }
    }
}
