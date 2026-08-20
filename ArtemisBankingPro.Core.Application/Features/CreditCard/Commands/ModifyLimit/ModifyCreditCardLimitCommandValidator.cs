using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.ModifyLimit
{
    public class ModifyCreditCardLimitCommandValidator : AbstractValidator<ModifyCreditCardLimitCommand>
    {
        private readonly ICreditCardRepository _creditCardRepository;

        public ModifyCreditCardLimitCommandValidator(ICreditCardRepository creditCardRepository)
        {
            _creditCardRepository = creditCardRepository;

            RuleFor(x => x.CreditLimit)
                .GreaterThan(0).WithMessage("The credit limit must be greater than zero.");

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
                        context.AddFailure(nameof(command.CreditCardId), "Cancelled credit cards cannot be modified.");
                        return;
                    }

                    if (command.CreditLimit < card.CurrentDebt)
                    {
                        context.AddFailure(nameof(command.CreditLimit), "The credit limit cannot be lower than the current outstanding debt.");
                    }
                });
        }
    }
}
