using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Commands.Asign
{
    public class AssignLoanCommandValidator : AbstractValidator<AssignLoanCommand>
    {
        private static readonly int[] AllowedTerms = [6, 12, 18, 24, 36, 48, 60];

        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly ILoanRepository _loanRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;

        public AssignLoanCommandValidator(IBasicUserInfoService basicUserInfoService, ILoanRepository loanRepository,
            ISavingsAccountRepository savingsAccountRepository)
        {
            _basicUserInfoService = basicUserInfoService;
            _loanRepository = loanRepository;
            _savingsAccountRepository = savingsAccountRepository;

            RuleFor(x => x.CapitalAmount)
                .GreaterThan(0).WithMessage("The loan amount must be greater than zero.");

            RuleFor(x => x.AnnualInterestRate)
                .GreaterThanOrEqualTo(0).WithMessage("The annual interest rate cannot be negative.");

            RuleFor(x => x.TermInMonths)
                .Must(term => AllowedTerms.Contains(term))
                .WithMessage("The selected term is not valid.");

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("The client id is required.")
                .MustAsync(async (clientId, cancellation) =>
                {
                    var isActive = await _basicUserInfoService.IsClientActiveAsync(clientId);
                    return isActive == true;
                }).WithMessage("The client must be active.")
                .MustAsync(async (clientId, cancellation) =>
                {
                    var hasActiveLoan = await _loanRepository.ClientHasActiveLoanAsync(clientId);
                    return !hasActiveLoan;
                }).WithMessage("This client already has an active loan assigned.")
                .MustAsync(async (clientId, cancellation) =>
                {
                    var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(clientId);
                    return principalAccount is not null && principalAccount.Status == SavingsAccountStatus.Active;
                }).WithMessage("The client does not have an active primary savings account to receive the loan disbursement.");
        }
    }
}
