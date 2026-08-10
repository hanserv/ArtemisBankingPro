using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ISavingsAccountRepository : IGenericRepository<SavingsAccount>
    {
        Task<bool> AccountNumberExistsAsync(string accountNumber);
        Task<SavingsAccount?> GetPrincipalAccountByClientIdAsync(string clientId);
    }
}
