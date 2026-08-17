using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface IBeneficiaryRepository : IGenericRepository<Beneficiary>
    {
        Task<bool> ExistsAsync(string clientId, int savingsAccountId);
        Task<List<Beneficiary>> GetByClientIdAsync(string clientId);
    }
}
