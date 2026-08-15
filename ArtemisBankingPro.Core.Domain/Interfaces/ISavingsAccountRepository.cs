using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ISavingsAccountRepository : IGenericRepository<SavingsAccount>
    {
        Task<bool> AccountNumberExistsAsync(string accountNumber);
        Task<SavingsAccount?> GetPrincipalAccountByClientIdAsync(string clientId);
        Task<SavingsAccount?> GetByAccountNumberAsync(string accountNumber);
        Task<int> CountActiveAsync();
        Task<List<SavingsAccount>> GetActiveByClientIdAsync(string clientId);
    }
}
