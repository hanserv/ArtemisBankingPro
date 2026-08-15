using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        Task<int> CountByCategoryAsync(TransactionCategory category, DateTime? date = null, string? performedByUserId = null);
        Task<int> CountPaymentsAsync(DateTime? date = null, string? performedByUserId = null);
        Task<int> CountTransactionsAsync(DateTime? date = null, string? performedByUserId = null, bool onlyApproved = false);
        Task<List<Transaction>> GetByAccountIdAsync(int accountId);
    }
}
