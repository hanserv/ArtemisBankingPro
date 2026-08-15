using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ArtemisBankingProContext context) : base(context)
        {
        }

        public async Task<int> CountTransactionsAsync(DateTime? date = null, string? performedByUserId = null, bool onlyApproved = false)
        {
            var query = _dbSet.AsQueryable();

            if (onlyApproved)
            {
                query = query.Where(t => t.Status == TransactionStatus.Approved);
            }

            if (date is not null)
            {
                query = query.Where(t => t.CreatedAt.Date == date.Value.Date);
            }

            if (performedByUserId is not null)
            {
                query = query.Where(t => t.PerformedByUserId == performedByUserId);
            }

            return await query.CountAsync();
        }

        public async Task<int> CountPaymentsAsync(DateTime? date = null, string? performedByUserId = null)
        {
            var query = _dbSet.Where(t =>
                (t.Category == TransactionCategory.LoanPayment || t.Category == TransactionCategory.CreditCardPayment)
                && t.Status == TransactionStatus.Approved);

            if (date is not null)
            {
                query = query.Where(t => t.CreatedAt.Date == date.Value.Date);
            }

            if (performedByUserId is not null)
            {
                query = query.Where(t => t.PerformedByUserId == performedByUserId);
            }

            return await query.CountAsync();
        }

        public async Task<int> CountByCategoryAsync(TransactionCategory category, DateTime? date = null, string? performedByUserId = null)
        {
            var query = _dbSet.Where(t => t.Category == category && t.Status == TransactionStatus.Approved);

            if (date is not null)
            {
                query = query.Where(t => t.CreatedAt.Date == date.Value.Date);
            }

            if (performedByUserId is not null)
            {
                query = query.Where(t => t.PerformedByUserId == performedByUserId);
            }

            return await query.CountAsync();
        }

        public async Task<List<Transaction>> GetByAccountIdAsync(int accountId)
        {
            return await _dbSet
                    .Where(t => t.SavingsAccountId == accountId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
        }
    }
}
