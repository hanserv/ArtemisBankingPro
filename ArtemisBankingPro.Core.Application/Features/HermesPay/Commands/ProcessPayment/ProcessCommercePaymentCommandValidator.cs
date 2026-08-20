using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using FluentValidation;

namespace ArtemisBankingPro.Core.Application.Features.HermesPay.Commands.ProcessPayment
{
    public class ProcessCommercePaymentCommandValidator : AbstractValidator<ProcessCommercePaymentCommand>
    {
        private readonly ICreditCardRepository _creditCardRepository;
        private readonly ICommerceRepository _commerceRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly ISavingsAccountRepository _savingsAccountRepository;

        public ProcessCommercePaymentCommandValidator(ICreditCardRepository creditCardRepository, ICommerceRepository commerceRepository,
            IBasicUserInfoService basicUserInfoService, ISavingsAccountRepository savingsAccountRepository)
        {
            _creditCardRepository = creditCardRepository;
            _commerceRepository = commerceRepository;
            _basicUserInfoService = basicUserInfoService;
            _savingsAccountRepository = savingsAccountRepository;

            RuleFor(x => x.CardNumber)
                .NotEmpty().WithMessage("The card number is required.")
                .Matches(@"^\d{16}$").WithMessage("The card number must contain exactly 16 digits.");

            RuleFor(x => x.MonthExpirationCard)
                .NotEmpty().WithMessage("The expiration month is required.")
                .Matches(@"^(0[1-9]|1[0-2])$").WithMessage("The expiration month must be a valid value between 01 and 12.");

            RuleFor(x => x.YearExpirationCard)
                .NotEmpty().WithMessage("The expiration year is required.")
                .Matches(@"^\d{4}$").WithMessage("The expiration year must be in YYYY format.");

            RuleFor(x => x.Cvc)
                .NotEmpty().WithMessage("The CVC is required.")
                .Matches(@"^\d{3}$").WithMessage("The CVC must contain exactly 3 digits.");

            RuleFor(x => x.TransactionAmount)
                .GreaterThan(0).WithMessage("The transaction amount must be greater than zero.");

            RuleFor(x => x)
                .CustomAsync(async (command, context, cancellation) =>
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(command.CardNumber, @"^\d{16}$"))
                    {
                        return;
                    }

                    var card = await _creditCardRepository.GetByCardNumberAsync(command.CardNumber);
                    if (card is null)
                    {
                        context.AddFailure(nameof(command.CardNumber), "The card does not exist.");
                        return;
                    }

                    if (card.Status != CreditCardStatus.Active)
                    {
                        context.AddFailure(nameof(command.CardNumber), "The card is not active.");
                        return;
                    }

                    var storedExpirationParts = card.ExpirationDate.Split('/');
                    var storedMonth = storedExpirationParts[0];
                    var storedYear = "20" + storedExpirationParts[1];

                    if (storedMonth != command.MonthExpirationCard || storedYear != command.YearExpirationCard)
                    {
                        context.AddFailure(nameof(command.CardNumber), "The card expiration data does not match.");
                        return;
                    }

                    var lastDayOfMonth = new DateTime(int.Parse(storedYear), int.Parse(storedMonth),
                        DateTime.DaysInMonth(int.Parse(storedYear), int.Parse(storedMonth)));

                    if (DateTime.UtcNow.Date > lastDayOfMonth)
                    {
                        context.AddFailure(nameof(command.CardNumber), "The card is expired.");
                        return;
                    }

                    if (Sha256Helper.Hash(command.Cvc) != card.CvcHash)
                    {
                        context.AddFailure(nameof(command.Cvc), "The CVC does not match.");
                        return;
                    }

                    var commerce = await _commerceRepository.GetByIdAsync(command.CommerceId);
                    if (commerce is null)
                    {
                        return;
                    }

                    if (!commerce.IsActive)
                    {
                        context.AddFailure(nameof(command.CommerceId), "The commerce is not active.");
                        return;
                    }

                    var commerceUserId = await _basicUserInfoService.GetUserIdByCommerceIdAsync(commerce.Id);
                    if (commerceUserId is null)
                    {
                        context.AddFailure(nameof(command.CommerceId), "The commerce does not have an associated user.");
                        return;
                    }

                    var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(commerceUserId);
                    if (principalAccount is null || principalAccount.Status != SavingsAccountStatus.Active)
                    {
                        context.AddFailure(nameof(command.CommerceId), "The commerce's associated user does not have an active principal savings account.");
                    }
                });
        }
    }
}
