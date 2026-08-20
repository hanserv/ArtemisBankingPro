using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.AssignSecondary
{
    public class AssignSecondaryAccountCommandValidator : AbstractValidator<AssignSecondaryAccountCommand>
    {
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly ISavingsAccountRepository _savingsAccountRepository;

        public AssignSecondaryAccountCommandValidator(IBasicUserInfoService basicUserInfoService, ISavingsAccountRepository savingsAccountRepository)
        {
            _basicUserInfoService = basicUserInfoService;
            _savingsAccountRepository = savingsAccountRepository;

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("You must select a client to continue.");

            RuleFor(x => x.InitialBalance)
                .GreaterThanOrEqualTo(0).WithMessage("The initial balance cannot be negative.");

            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellation) =>
                {
                    if (string.IsNullOrWhiteSpace(command.ClientId))
                    {
                        return;
                    }

                    var isActive = await _basicUserInfoService.IsClientActiveAsync(command.ClientId);

                    if (isActive is null)
                    {
                        return;
                    }

                    if (isActive == false)
                    {
                        context.AddFailure(nameof(command.ClientId), "Savings accounts can only be assigned to active clients.");
                        return;
                    }

                    var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(command.ClientId);

                    if (principalAccount is null || principalAccount.Status != SavingsAccountStatus.Active)
                    {
                        context.AddFailure(nameof(command.ClientId), "The client must have an active principal savings account before a secondary account can be assigned.");
                    }
                });
        }
    }
}
