using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.AssignSecondary
{
    /// <summary>
    /// Parameters required to assign a secondary savings account to a client
    /// </summary>
    public class AssignSecondaryAccountCommand : IRequest<SavingsAccountDto>
    {
        /// <example>20</example>
        [SwaggerParameter(Description = "Identifier of the client to whom the secondary account will be assigned.")]
        public required string ClientId { get; set; }

        /// <example>5000.00</example>
        [SwaggerParameter(Description = "Initial balance of the account. Can be RD$0.00 but cannot be negative.")]
        public required decimal InitialBalance { get; set; }
        public string CreatedByAdminId { get; set; } = string.Empty;
    }

    public class AssignSecondaryAccountCommandHandler : IRequestHandler<AssignSecondaryAccountCommand, SavingsAccountDto>
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountNumberGenerator _accountNumberGenerator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;
        private readonly ILogger<AssignSecondaryAccountCommandHandler> _logger;

        public AssignSecondaryAccountCommandHandler(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IAccountNumberGenerator accountNumberGenerator, IUnitOfWork unitOfWork, IBasicUserInfoService basicUserInfoService,
            IMapper mapper, ILogger<AssignSecondaryAccountCommandHandler> logger)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _accountNumberGenerator = accountNumberGenerator;
            _unitOfWork = unitOfWork;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<SavingsAccountDto> Handle(AssignSecondaryAccountCommand request, CancellationToken cancellationToken)
        {
            var client = await _basicUserInfoService.GetBasicInfoAsync(request.ClientId);

            if (client is null)
            {
                throw new ApiException("The selected client does not exist.", (int)HttpStatusCode.NotFound);
            }

            string accountNumber;
            try
            {
                accountNumber = await _accountNumberGenerator.GenerateAsync();
            }
            catch (InvalidOperationException)
            {
                throw new ApiException("It was not possible to generate a unique account number. Please try again.", (int)HttpStatusCode.Conflict);
            }

            var hasInitialCredit = request.InitialBalance > 0;

            var account = new Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = request.ClientId,
                Balance = request.InitialBalance,
                Type = SavingsAccountType.Secondary,
                Status = SavingsAccountStatus.Active,
                CreatedByAdminId = request.CreatedByAdminId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _savingsAccountRepository.AddAsync(account);

                if (hasInitialCredit)
                {
                    await _transactionRepository.AddAsync(new Domain.Entities.Transaction
                    {
                        Id = 0,
                        SavingsAccountId = account.Id,
                        Amount = request.InitialBalance,
                        Type = TransactionType.Credit,
                        Category = TransactionCategory.AccountOpening,
                        Origin = "Account Opening",
                        Beneficiary = account.AccountNumber,
                        Status = TransactionStatus.Approved,
                        PerformedByUserId = request.CreatedByAdminId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            });

            _logger.LogInformation(hasInitialCredit
                ? "Secondary account {AccountNumber} assigned to client {ClientId} by administrator {AdminId}, opened with an initial credit of {InitialBalance:C}."
                : "Secondary account {AccountNumber} assigned to client {ClientId} by administrator {AdminId} with zero balance.",
                account.AccountNumber, request.ClientId, request.CreatedByAdminId, request.InitialBalance);

            var dto = _mapper.Map<SavingsAccountDto>(account);
            dto.ClientFullName = client.FullName;

            return dto;
        }
    }
}
