using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetAll
{
    public class GetAllLoansQueryValidator : AbstractValidator<GetAllLoansQuery>
    {
        public GetAllLoansQueryValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("The page parameter must be greater than zero.");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("The pageSize parameter must be greater than zero.");
        }
    }
}
