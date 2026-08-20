using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Commands.ModifyRate
{
    public class ModifyLoanRateCommandValidator : AbstractValidator<ModifyLoanRateCommand>
    {
        private readonly ILoanRepository _loanRepository;

        public ModifyLoanRateCommandValidator(ILoanRepository loanRepository)
        {
            _loanRepository = loanRepository;

            RuleFor(x => x.AnnualInterestRate)
                .GreaterThanOrEqualTo(0).WithMessage("The annual interest rate cannot be negative.");

            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellation) =>
                {
                    var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                        .FirstOrDefaultAsync(l => l.Id == command.LoanId, cancellation);

                    if (loan is null)
                    {
                        // Existence is validated in the Handler (404), not here.
                        return;
                    }

                    if (loan.Status != LoanStatus.Active)
                    {
                        context.AddFailure(nameof(command.LoanId), "Only active loans can have their interest rate modified.");
                        return;
                    }

                    var hasEligibleInstallments = loan.Installments.Any(
                        i => i.Status == InstallmentStatus.Pending && i.DueDate > DateTime.UtcNow);

                    if (!hasEligibleInstallments)
                    {
                        context.AddFailure(nameof(command.LoanId), "There are no future pending installments to recalculate.");
                    }
                });
        }
    }
}
