using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Update
{
    public class UpdateCommerceCommandValidator : AbstractValidator<UpdateCommerceCommand>
    {
        public UpdateCommerceCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("The commerce name is required.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("The email is required.")
                .EmailAddress().WithMessage("The email must have a valid format.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("The phone number is required.");

            RuleFor(x => x.Rnc)
                .NotEmpty().WithMessage("The RNC is required.");
        }
    }
}
