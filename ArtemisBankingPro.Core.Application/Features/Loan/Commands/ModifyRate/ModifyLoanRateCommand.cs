using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Commands.ModifyRate
{
    /// <summary>
    /// Parameters required to modify a loan's annual interest rate
    /// </summary>
    public class ModifyLoanRateCommand : IRequest<Unit>
    {
        public int LoanId { get; set; }

        /// <example>10.50</example>
        [SwaggerParameter(Description = "The new annual interest rate to apply to the loan.")]
        public required decimal AnnualInterestRate { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class ModifyLoanRateCommandHandler : IRequestHandler<ModifyLoanRateCommand, Unit>
    {
        private readonly ILoanRepository _loanRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ModifyLoanRateCommandHandler> _logger;

        public ModifyLoanRateCommandHandler(ILoanRepository loanRepository, IBasicUserInfoService basicUserInfoService,
            IEmailService emailService, ILogger<ModifyLoanRateCommandHandler> logger)
        {
            _loanRepository = loanRepository;
            _basicUserInfoService = basicUserInfoService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Unit> Handle(ModifyLoanRateCommand request, CancellationToken cancellationToken)
        {
            var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                .FirstOrDefaultAsync(l => l.Id == request.LoanId, cancellationToken);

            if (loan is null)
            {
                throw new ApiException("The selected loan does not exist.", (int)HttpStatusCode.NotFound);
            }

            var allInstallments = loan.Installments.OrderBy(i => i.InstallmentNumber).ToList();

            var eligibleInstallments = allInstallments
                .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate > DateTime.UtcNow)
                .ToList();

            var outstandingPrincipal = allInstallments
                .Where(i => i.Status == InstallmentStatus.Pending || i.Status == InstallmentStatus.PartiallyPaid)
                .Sum(i => i.PrincipalAmount);

            FrenchAmortizationCalculator.RecalculateInstallments(eligibleInstallments, outstandingPrincipal, request.AnnualInterestRate);

            loan.AnnualInterestRate = request.AnnualInterestRate;
            loan.PendingAmount = allInstallments
                .Where(i => i.Status != InstallmentStatus.Paid)
                .Sum(i => i.RemainingBalance);

            await _loanRepository.UpdateAsync(loan);

            _logger.LogInformation("Annual interest rate for loan {LoanNumber} updated to {NewRate}% by administrator {AdminId}. {RecalculatedCount} future installments recalculated.",
                loan.LoanNumber, request.AnnualInterestRate, request.AdminId, eligibleInstallments.Count);

            await TrySendRateUpdateEmailAsync(loan, eligibleInstallments[0]);
            return Unit.Value;
        }

        #region Private Methods
        private async Task<bool> TrySendRateUpdateEmailAsync(Domain.Entities.Loan loan, LoanInstallment nextInstallment)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(loan.ClientId);

            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client!.Email,
                    Subject = "Loan interest rate update",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>The interest rate of your loan <strong>{loan.LoanNumber}</strong> has been updated.</p>
                        <p>New annual interest rate: <strong>{loan.AnnualInterestRate}%</strong></p>
                        <p>New next installment amount: <strong>RD$ {nextInstallment.InstallmentAmount:N2}</strong></p>
                        <p>Next installment due date: <strong>{nextInstallment.DueDate:MM/dd/yyyy}</strong></p>
                        <p style="font-size: 14px; color: #6c757d;">This change only applies to future pending installments.</p>
                    """
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rate update email for loan {LoanNumber} to client {ClientId}.", loan.LoanNumber, loan.ClientId);
                return false;
            }
        }
        #endregion
    }
}
