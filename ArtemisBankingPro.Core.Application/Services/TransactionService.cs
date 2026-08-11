using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ISavingsAccountRepository _savingsAccountRepository;
        private readonly IMapper _mapper;

        public TransactionService(ITransactionRepository transactionRepository, ISavingsAccountRepository savingsAccountRepository,
            IMapper mapper)
        {
            _transactionRepository = transactionRepository;
            _savingsAccountRepository = savingsAccountRepository;
            _mapper = mapper;
        }

        public async Task<Result<PagedResult<TransactionDto>>> GetAccountTransactionsAsync(int accountId, int page, int pageSize)
        {
            if (page <= 0)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The page parameter must be greater than zero.");
            }

            if (pageSize <= 0)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The pageSize parameter must be greater than zero.");
            }

            if (pageSize > 20)
            {
                pageSize = 20;
            }

            var accountExists = await _savingsAccountRepository.GetByIdAsync(accountId) is not null;

            if (!accountExists)
            {
                return Result<PagedResult<TransactionDto>>.Failure(error: "The selected account does not exist.");
            }

            var query = _transactionRepository.GetAllQuery()
                .Where(t => t.SavingsAccountId == accountId);

            var totalRecords = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = _mapper.Map<List<TransactionDto>>(transactions);

            return Result<PagedResult<TransactionDto>>.Success(new PagedResult<TransactionDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords
            });
        }
    }
}
