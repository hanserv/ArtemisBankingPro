using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class BeneficiaryRepository : GenericRepository<Beneficiary>, IBeneficiaryRepository
    {
        public BeneficiaryRepository(ArtemisBankingProContext context) : base(context)
        {
        }

        public async Task<List<Beneficiary>> GetByClientIdAsync(string clientId)
        {
            return await _dbSet
                    .Include(b => b.SavingsAccount)
                    .Where(b => b.ClientId == clientId)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();
        }

        public async Task<bool> ExistsAsync(string clientId, int savingsAccountId)
            => await _dbSet.AnyAsync(b => b.ClientId == clientId && b.SavingsAccountId == savingsAccountId);
        
    }
}
