using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class LoanService : ILoanService
    {
        private static readonly int[] AllowedTerms = [6, 12, 18, 24, 30, 36, 42, 48, 54, 60];

        private readonly ILoanRepository _loanRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IFinancialSummaryService _financialSummaryService;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly ILoanNumberGenerator _loanNumberGenerator;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<LoanService> _logger;
        private readonly IAccountServiceForWebApp _accountService;

        public LoanService(ILoanRepository loanRepository, ISavingsAccountRepository savingsAccountRepository,
            ITransactionRepository transactionRepository, IFinancialSummaryService financialSummaryService,
            IBasicUserInfoService basicUserInfoService, ILoanNumberGenerator loanNumberGenerator,
            IEmailService emailService, IUnitOfWork unitOfWork,
            IMapper mapper, ILogger<LoanService> logger, IAccountServiceForWebApp accountService)
        {
            _loanRepository = loanRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _financialSummaryService = financialSummaryService;
            _basicUserInfoService = basicUserInfoService;
            _loanNumberGenerator = loanNumberGenerator;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _accountService = accountService;
        }

        public async Task<Result<LoanDto>> GetByIdAsync(int id)
        {
            var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                        .FirstOrDefaultAsync(l => l.Id == id);

            if (loan is null)
            {
                return Result<LoanDto>.Failure("The requested loan does not exist.");
            }

            var dto = _mapper.Map<LoanDto>(loan);
            dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(loan.ClientId);

            return Result<LoanDto>.Success(dto);
        }

        public async Task<Result<PagedResult<LoanDto>>> GetPagedAsync(LoanFilterDto filter)
        {
            if (filter.Page <= 0)
            {
                return Result<PagedResult<LoanDto>>.Failure("The page parameter must be greater than zero.");
            }

            if (filter.PageSize <= 0)
            {
                return Result<PagedResult<LoanDto>>.Failure("The pageSize parameter must be greater than zero.");
            }

            if (filter.PageSize > 20)
            {
                filter.PageSize = 20;
            }

            string? clientId = null;

            if (!string.IsNullOrWhiteSpace(filter.Identification))
            {
                clientId = await _basicUserInfoService.GetUserIdByIdentificationAsync(filter.Identification);
                if (clientId is null)
                {
                    return Result<PagedResult<LoanDto>>.Failure("There is no client registered with this identification.");
                }
            }

            var query = _loanRepository.GetAllQueryInclude(["Installments"]);

            if (filter.Status is not null)
            {
                query = query.Where(l => l.Status == filter.Status);
            }

            if (clientId is not null)
            {
                query = query.Where(l => l.ClientId == clientId);
            }

            var totalRecords = await query.CountAsync();

            if (clientId is not null && totalRecords == 0)
            {
                return Result<PagedResult<LoanDto>>.Failure("This client has no loans registered.");
            }

            var orderedQuery = clientId is not null && filter.Status is null
                ? query.OrderBy(l => l.Status == LoanStatus.Completed).ThenByDescending(l => l.CreatedAt)
                : query.OrderByDescending(l => l.CreatedAt);

            var loans = await orderedQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var items = new List<LoanDto>();
            foreach (var loan in loans)
            {
                var dto = _mapper.Map<LoanDto>(loan);
                dto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(loan.ClientId);
                items.Add(dto);
            }

            return Result<PagedResult<LoanDto>>.Success(new PagedResult<LoanDto>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalRecords = totalRecords
            });
        }

        public async Task<Result<LoanDetailsDto>> GetDetailsAsync(int id)
        {
            var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                        .FirstOrDefaultAsync(l => l.Id == id);

            if (loan is null)
            {
                return Result<LoanDetailsDto>.Failure("The requested loan does not exist.");
            }

            var loanDto = _mapper.Map<LoanDto>(loan);
            loanDto.ClientFullName = await _basicUserInfoService.GetFullNameAsync(loan.ClientId);

            var installments = loan.Installments
                    .OrderBy(i => i.InstallmentNumber)
                    .ToList();

            return Result<LoanDetailsDto>.Success(new LoanDetailsDto
            {
                Loan = loanDto,
                Installments = _mapper.Map<List<LoanInstallmentDto>>(installments)
            });
        }

        public async Task<Result<List<ClientForAssignmentDto>>> GetClientsEligibleForLoanAsync(string? identification)
        {
            var clientsResult = await _accountService.GetClientsForAssignmentAsync(identification);

            if (!clientsResult.IsSuccess)
            {
                return clientsResult;
            }

            var clientIdsWithActiveLoan = await _loanRepository.GetAllQuery()
                .Where(l => l.Status == LoanStatus.Active)
                .Select(l => l.ClientId)
                .ToListAsync();

            var eligibleClients = clientsResult.Value!
                .Where(c => !clientIdsWithActiveLoan.Contains(c.Id))
                .ToList();

            return Result<List<ClientForAssignmentDto>>.Success(eligibleClients);
        }

        public async Task<Result<AssignLoanResultDto>> AssignAsync(AssignLoanDto dto)
        {
            if (!AllowedTerms.Contains(dto.TermInMonths))
            {
                return Result<AssignLoanResultDto>.Failure("The selected term is not valid.");
            }

            if (dto.CapitalAmount <= 0)
            {
                return Result<AssignLoanResultDto>.Failure("The loan amount must be greater than zero.");
            }

            if (dto.AnnualInterestRate < 0)
            {
                return Result<AssignLoanResultDto>.Failure("The annual interest rate cannot be negative.");
            }

            var client = await _basicUserInfoService.GetBasicInfoAsync(dto.ClientId);
            if (client is null)
            {
                return Result<AssignLoanResultDto>.Failure("The selected client does not exist.");
            }

            var isClientActive = await _basicUserInfoService.IsClientActiveAsync(dto.ClientId);
            if (isClientActive != true)
            {
                return Result<AssignLoanResultDto>.Failure("The client must be active.");
            }

            if (await _loanRepository.ClientHasActiveLoanAsync(dto.ClientId))
            {
                return Result<AssignLoanResultDto>.Failure("This client already has an active loan assigned.");
            }

            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(dto.ClientId);
            if (principalAccount is null || principalAccount.Status != SavingsAccountStatus.Active)
            {
                return Result<AssignLoanResultDto>.Failure("The client does not have an active primary savings account to receive the loan disbursement.");
            }

            var schedule = FrenchAmortizationCalculator.GenerateSchedule(dto.CapitalAmount, dto.AnnualInterestRate, dto.TermInMonths, DateTime.UtcNow);

            var totalToPay = schedule.Sum(i => i.InstallmentAmount);

            var riskWarning = await _financialSummaryService.CheckIfHighRiskAsync(dto.ClientId, totalToPay);

            if (riskWarning is not null && !dto.RiskWarningAccepted)
            {
                return Result<AssignLoanResultDto>.Success(new AssignLoanResultDto
                {
                    RequiresRiskConfirmation = true,
                    RiskWarning = riskWarning
                });
            }

            var loanNumber = await _loanNumberGenerator.GenerateAsync();

            var loan = _mapper.Map<Loan>(dto);
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

                principalAccount.Balance += dto.CapitalAmount;
                await _savingsAccountRepository.UpdateAsync(principalAccount);

                await _transactionRepository.AddAsync(new Transaction
                {
                    Id = 0,
                    SavingsAccountId = principalAccount.Id,
                    Amount = dto.CapitalAmount,
                    Type = TransactionType.Credit,
                    Category = TransactionCategory.LoanDisbursement,
                    Origin = $"Loan {loanNumber}",
                    Beneficiary = principalAccount.AccountNumber,
                    Status = TransactionStatus.Approved,
                    PerformedByUserId = dto.AdminId,
                    CreatedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Loan {LoanNumber} assigned and disbursed for client {ClientId} by admin {AdminId}. Capital: {Capital}", loanNumber, dto.ClientId, dto.AdminId, dto.CapitalAmount);

            var loanDto = _mapper.Map<LoanDto>(loan);
            loanDto.ClientFullName = client.FullName;

            var monthlyInstallment = schedule[0].InstallmentAmount;

            var emailSent = await TrySendApprovalEmailAsync(client, loanNumber, dto.CapitalAmount, dto.TermInMonths, dto.AnnualInterestRate, monthlyInstallment);

            if (!emailSent)
            {
                return Result<AssignLoanResultDto>.Success(new AssignLoanResultDto { Loan = loanDto }, message: "The loan was assigned successfully, but the notification email could not be sent.");
            }

            return Result<AssignLoanResultDto>.Success(new AssignLoanResultDto { Loan = loanDto }, message: "The loan was assigned successfully.");
        }

        public async Task<Result> ValidateClientForAssignmentAsync(string? clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Result.Failure(error: "You must select a client to continue.");
            }

            var isActive = await _basicUserInfoService.IsClientActiveAsync(clientId);

            if (isActive is null or false)
            {
                return Result.Failure(error: "Loans can only be assigned to active clients.");
            }

            var hasActiveLoan = await _loanRepository.GetAllQuery()
                .AnyAsync(l => l.ClientId == clientId && l.Status == LoanStatus.Active);

            if (hasActiveLoan)
            {
                return Result.Failure(error: "This client already has an active loan assigned.");
            }

            var principalAccount = await _savingsAccountRepository.GetPrincipalAccountByClientIdAsync(clientId);

            if (principalAccount is null || principalAccount.Status != SavingsAccountStatus.Active)
            {
                return Result.Failure(error: "The client must have an active principal savings account before a loan can be assigned.");
            }

            return Result.Success();
        }

        public async Task<Result> ModifyRateAsync(ModifyLoanRateDto dto, string performedByAdminId)
        {
            var loan = await _loanRepository.GetAllQueryInclude(["Installments"])
                        .FirstOrDefaultAsync(l => l.Id == dto.LoanId);

            if (loan is null)
            {
                return Result.Failure("The selected loan does not exist.");
            }

            if (loan.Status != LoanStatus.Active)
            {
                return Result.Failure("Only active loans can have their interest rate modified.");
            }

            if (dto.AnnualInterestRate < 0)
            {
                return Result.Failure("The annual interest rate cannot be negative.");
            }

            var allInstallments = loan.Installments.OrderBy(i => i.InstallmentNumber).ToList();

            var eligibleInstallments = allInstallments
                    .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate > DateTime.UtcNow)
                    .ToList();

            if (eligibleInstallments.Count == 0)
            {
                return Result.Failure("There are no future pending installments to recalculate.");
            }

            var outstandingPrincipal = allInstallments
                    .Where(i => i.Status == InstallmentStatus.Pending || i.Status == InstallmentStatus.PartiallyPaid)
                    .Sum(i => i.PrincipalAmount);

            FrenchAmortizationCalculator.RecalculateInstallments(eligibleInstallments, outstandingPrincipal, dto.AnnualInterestRate);

            loan.AnnualInterestRate = dto.AnnualInterestRate;
            loan.PendingAmount = allInstallments
                .Where(i => i.Status != InstallmentStatus.Paid)
                .Sum(i => i.InstallmentAmount);

            await _loanRepository.UpdateAsync(loan);

            _logger.LogInformation(
                "Annual interest rate for loan {LoanNumber} updated to {NewRate}% by administrator {AdminId}. {RecalculatedCount} future installments recalculated.",
                loan.LoanNumber, dto.AnnualInterestRate, performedByAdminId, eligibleInstallments.Count);

            var emailSent = await TrySendRateUpdateEmailAsync(loan, eligibleInstallments[0]);

            return Result.Success(emailSent
                    ? "The interest rate has been updated successfully."
                    : "The interest rate was updated successfully, but the notification email could not be sent.");
        }

        public async Task<int> MarkOverdueInstallmentsAsync()
        {
            var updatedCount = await _loanRepository.MarkOverdueInstallmentsAsync();

            if (updatedCount > 0)
            {
                _logger.LogInformation("{Count} loan installment(s) marked as late.", updatedCount);
            }

            return updatedCount;
        }

        #region Private Methods

        private async Task<bool> TrySendApprovalEmailAsync(UserBasicInfoDto client, string loanNumber, decimal capitalAmount, int termInMonths,decimal annualRate, decimal monthlyInstallment)
        {
            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto
                {
                    To = client.Email,
                    Subject = "Loan approved",
                    BodyHtml = $"""
                        <h3 style="font-weight: normal;">Hello <span style="font-weight: bold;">{client.FullName}</span></h3>
                        <p>Your loan has been approved successfully.</p>
                        <p>Loan number: <strong>{loanNumber}</strong></p>
                        <p>Approved amount: <strong>RD$ {capitalAmount:N2}</strong></p>
                        <p>Term: <strong>{termInMonths} months</strong></p>
                        <p>Annual interest rate: <strong>{annualRate}%</strong></p>
                        <p>Monthly installment: <strong>RD$ {monthlyInstallment:N2}</strong></p>
                        <p>The approved amount has been deposited into your primary savings account.</p>
                        <p style="font-size: 14px; color: #6c757d;">If you do not recognize this operation, please contact the bank.</p>
                    """
                });

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Loan approval email for loan {LoanNumber} was not sent: {Error}", loanNumber, result.Error);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send loan approval email for loan {LoanNumber} to client {ClientId}.", loanNumber, client.Id);

                return false;
            }
        }

        private async Task<bool> TrySendRateUpdateEmailAsync(Loan loan, LoanInstallment nextInstallment)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(loan.ClientId);

            try
            {
                var result = await _emailService.SendAsync(new EmailRequestDto
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

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Rate update email for loan {LoanNumber} was not sent: {Error}", loan.LoanNumber, result.Error);
                }

                return result.IsSuccess;
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
