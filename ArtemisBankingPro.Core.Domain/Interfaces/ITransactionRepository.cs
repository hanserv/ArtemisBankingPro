using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        Task<int> CountTransactionsAsync(DateTime? date = null);
        Task<int> CountPaymentsAsync(DateTime? date = null);
    }
}
