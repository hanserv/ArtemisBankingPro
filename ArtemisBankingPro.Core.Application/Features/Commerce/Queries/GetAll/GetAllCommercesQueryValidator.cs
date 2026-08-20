using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetAll
{
    public class GetAllCommercesQueryValidator : AbstractValidator<GetAllCommercesQuery>
    {
        public GetAllCommercesQueryValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("The page parameter must be greater than zero.");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("The pageSize parameter must be greater than zero.");
        }
    }
}
