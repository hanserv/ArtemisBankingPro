using System.Net;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetTransactions
{
    /// <summary>
    /// Filter parameters for retrieving the paginated transaction history of a savings account.
    /// </summary>
    public class GetSavingsAccountTransactionsQuery : IRequest<SavingsAccountTransactionHistoryDto>
    {
        public string AccountNumber { get; set; } = string.Empty;

        /// <example>1</example>
        [SwaggerParameter(Description = "Page number to retrieve.")]
        public int Page { get; set; } = 1;

        /// <example>20</example>
        [SwaggerParameter(Description = "Number of records per page. Maximum allowed is 20.")]
        public int PageSize { get; set; } = 20;
    }

    public class GetSavingsAccountTransactionsQueryHandler : IRequestHandler<GetSavingsAccountTransactionsQuery, SavingsAccountTransactionHistoryDto>
    {
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBasicUserInfoService _basicUserInfoService;
        private readonly IMapper _mapper;

        public GetSavingsAccountTransactionsQueryHandler(ISavingsAccountRepository savingsAccountRepository, ITransactionRepository transactionRepository,
            IBasicUserInfoService basicUserInfoService, IMapper mapper)
        {
            _savingsAccountRepository = savingsAccountRepository;
            _transactionRepository = transactionRepository;
            _basicUserInfoService = basicUserInfoService;
            _mapper = mapper;
        }

        public async Task<SavingsAccountTransactionHistoryDto> Handle(GetSavingsAccountTransactionsQuery request, CancellationToken cancellationToken)
        {
            if (request.PageSize > 20)
            {
                request.PageSize = 20;
            }

            var account = await _savingsAccountRepository.GetByAccountNumberAsync(request.AccountNumber);

            if (account is null)
            {
                throw new ApiException("The selected account does not exist.", (int)HttpStatusCode.NotFound);
            }

            var query = _transactionRepository.GetAllQuery()
                .Where(t => t.SavingsAccountId == account.Id);

            var totalRecords = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return new SavingsAccountTransactionHistoryDto
            {
                AccountNumber = account.AccountNumber,
                ClientFullName = await _basicUserInfoService.GetFullNameAsync(account.ClientId),
                Balance = account.Balance,
                Type = account.Type,
                Status = account.Status,
                Transactions = new PagedResult<TransactionDto>
                {
                    Items = _mapper.Map<List<TransactionDto>>(transactions),
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalRecords = totalRecords
                }
            };
        }
    }
}
