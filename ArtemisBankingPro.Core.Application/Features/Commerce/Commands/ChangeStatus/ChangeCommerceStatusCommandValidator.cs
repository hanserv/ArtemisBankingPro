using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Commands.ChangeStatus
{
    public class ChangeCommerceStatusCommandValidator : AbstractValidator<ChangeCommerceStatusCommand>
    {
        public ChangeCommerceStatusCommandValidator()
        {
            RuleFor(x => x.Status)
                .NotNull().WithMessage("The status field is required.");
        }
    }
}
