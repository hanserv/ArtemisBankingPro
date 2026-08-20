using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.Loan.Commands.Asign
{
    /// <summary>
    /// Parameters required to assign a new loan to a client
    /// </summary>
    public class AssignLoanCommand : IRequest<LoanCreatedResponseDto>
    {
        /// <example>20</example>
        [SwaggerParameter(Description = "The identifier of the client the loan will be assigned to.")]
        public required string ClientId { get; set; }

        /// <example>100000.00</example>
        [SwaggerParameter(Description = "The approved capital amount for the loan.")]
        public required decimal CapitalAmount { get; set; }

        /// <example>12.00</example>
        [SwaggerParameter(Description = "The annual interest rate applied to the loan.")]
        public required decimal AnnualInterestRate { get; set; }

        /// <example>12</example>
        [SwaggerParameter(Description = "The loan term expressed in months.")]
        public required int TermInMonths { get; set; }

        /// <example>false</example>
        [SwaggerParameter(Description = "Confirms the assignment even if the client is or becomes high-risk.")]
        public bool ConfirmHighRisk { get; set; } = false;

        [System.Text.Json.Serialization.JsonIgnore]
        public string AdminId { get; set; } = string.Empty;
    }

    public class AssignLoanCommandHandler : IRequestHandler<AssignLoanCommand, LoanCreatedResponseDto>
    {
        private readonly ILoanRepository _loanRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILoanNumberGenerator _loanNumberGenerator;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IFinancialSummaryService _financialSummaryService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IMapper _mapper;
        private readonly ILogger<AssignLoanCommandHandler> _logger;

        public AssignLoanCommandHandler(ILoanRepository loanRepository, ISavingsAccountRepository savingsAccountRepository,
            ITransactionRepository transactionRepository, ILoanNumberGenerator loanNumberGenerator,
            IBasicUserInfoService basicUserInfoService, IFinancialSummaryService financialSummaryService,
            IUnitOfWork unitOfWork, IEmailService emailService,
            IMapper mapper, ILogger<AssignLoanCommandHandler> logger)
        {
            _loanRepository = loanRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _loanNumberGenerator = loanNumberGenerator;
            _basicUserInfoService = basicUserInfoService;
            _financialSummaryService = financialSummaryService;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<LoanCreatedResponseDto> Handle(AssignLoanCommand request, CancellationToken cancellationToken)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(request.ClientId);

            if (client is null)
            {
                throw new ApiException("The selected client does not exist.", (int)HttpStatusCode.NotFound);
            }

            var schedule = FrenchAmortizationCalculator.GenerateSchedule(
                request.CapitalAmount, request.AnnualInterestRate, request.TermInMonths, DateTime.UtcNow);

            var totalToPay = schedule.Sum(i => i.InstallmentAmount);

            var riskWarning = await _financialSummaryService.CheckIfHighRiskAsync(request.ClientId, totalToPay);

            if (riskWarning is not null && !request.ConfirmHighRisk)
            {
                throw new HighRiskLoanException("Assigning this loan will make the client a high-risk client, since their debt will exceed the system's average threshold.",
                    riskWarning.RiskType.ToString(),
                    riskWarning.CurrentDebt,
                    riskWarning.ProjectedDebt,
                    riskWarning.AverageDebt);
            }

            var loanNumber = await _loanNumberGenerator.GenerateAsync();

            var loan = _mapper.Map<Domain.Entities.Loan>(request);
            loan.LoanNumber = loanNumber;
            loan.PendingAmount = totalToPay;
            loan.Status = LoanStatus.Active;
            loan.CreatedAt = DateTime.UtcNow;

            foreach (var installment in schedule)
            {
                loan.Installments.Add(installment);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _loanRepository.AddAsync(loan);

                var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(request.ClientId);
                principalAccount!.Balance += request.CapitalAmount;
                await _savingsAccountRepository.UpdateAsync(principalAccount);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = principalAccount.Id,
                    Amount = request.CapitalAmount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.LoanDisbursement,
                    Origin = $"Loan {loanNumber}",
                    Beneficiary = principalAccount.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = request.AdminId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Loan {LoanNumber} assigned and disbursed for client {ClientId} by admin {AdminId}. Capital: {Capital:C}.",
                loanNumber, request.ClientId, request.AdminId, request.CapitalAmount);

            var monthlyInstallment = schedule[0].InstallmentAmount;

            await TrySendApprovalEmailAsync(client, loanNumber, request.CapitalAmount, request.TermInMonths, request.AnnualInterestRate, monthlyInstallment);

            return new LoanCreatedResponseDto
            {
                Id = loan.Id,
                LoanNumber = loanNumber,
                ClientId = request.ClientId,
                ClientFullName = client.FullName,
                CapitalAmount = request.CapitalAmount,
                TermInMonths = request.TermInMonths,
                AnnualInterestRate = request.AnnualInterestRate,
                MonthlyInstallment = monthlyInstallment,
                TotalAmountToPay = totalToPay,
                Status = loan.Status,
                CreatedAt = loan.CreatedAt
            };
        }

        #region Private Methods
        private async Task<bool> TrySendApprovalEmailAsync(UserBasicInfoDto client, string loanNumber, decimal capital, int term, decimal rate, decimal installment)
        {
            try
            {
                await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client.Email,
                    Subject = "Loan approved",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>Your loan has been approved successfully.</p>
                        <p>Loan number: <strong>{loanNumber}</strong></p>
                        <p>Approved amount: <strong>RD$ {capital:N2}</strong></p>
                        <p>Term: <strong>{term} months</strong></p>
                        <p>Annual interest rate: <strong>{rate}%</strong></p>
                        <p>Monthly installment: <strong>RD$ {installment:N2}</strong></p>
                        <p>The approved amount has been deposited into your primary savings account.</p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval email for loan {LoanNumber} to client {ClientId}.", loanNumber, client.Id);
                return false;
            }
        }
        
        #endregion
    }
}
