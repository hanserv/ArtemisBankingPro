using ArtemisBankingPro.Core.Application.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Assign
{
    public class AssignCreditCardCommandValidator : AbstractValidator<AssignCreditCardCommand>
    {
        private readonly IBasicUserInfoService _basicUserInfoService;

        public AssignCreditCardCommandValidator(IBasicUserInfoService basicUserInfoService)
        {
            _basicUserInfoService = basicUserInfoService;

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("The client identifier is required.");

            RuleFor(x => x.CreditLimit)
                .GreaterThan(0).WithMessage("The credit limit must be greater than zero.");

            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellation) =>
                {
                    if (string.IsNullOrWhiteSpace(command.ClientId))
                    {
                        return;
                    }

                    var isActive = await _basicUserInfoService.IsClientActiveAsync(command.ClientId);

                    if (isActive == false)
                    {
                        context.AddFailure(nameof(command.ClientId), "Credit cards can only be assigned to active clients.");
                    }
                });
        }
    }
}
