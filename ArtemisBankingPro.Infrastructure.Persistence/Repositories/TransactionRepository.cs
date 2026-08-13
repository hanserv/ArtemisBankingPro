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

        public async Task<int> CountTransactionsAsync(DateTime? date = null)
        {
            var query = _dbSet.AsQueryable();
            if (date is not null)
            {
                query = query.Where(t => t.CreatedAt.Date == date.Value.Date);
            }

            return await query.CountAsync();
        }

        public async Task<int> CountPaymentsAsync(DateTime? date = null)
        {
            var query = _dbSet.Where(t =>t.Category == TransactionCategory.LoanPayment || t.Category == TransactionCategory.CreditCardPayment);

            if (date is not null)
            {
                query = query.Where(t => t.CreatedAt.Date == date.Value.Date);
            }

            return await query.CountAsync();
        }
    }
}
