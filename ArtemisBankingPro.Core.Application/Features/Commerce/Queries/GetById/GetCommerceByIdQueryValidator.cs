using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetById
{
    public class GetCommerceByIdQueryValidator : AbstractValidator<GetCommerceByIdQuery>
    {
        public GetCommerceByIdQueryValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("The id parameter must be greater than zero.");
        }
    }
}
