using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class SavingsAccountRepository : GenericRepository<SavingsAccount>, ISavingsAccountRepository
    {
        public SavingsAccountRepository(ArtemisBankingProContext context) : base(context)
        {
        }

        public async Task<bool> AccountNumberExistsAsync(string accountNumber)
            => await _dbSet.AnyAsync(a => a.AccountNumber == accountNumber);

        public async Task<SavingsAccount?> GetPrincipalAccountByClientIdAsync(string clientId)
            => await _dbSet.FirstOrDefaultAsync(a => a.ClientId == clientId && a.Type == SavingsAccountType.Principal);

        public async Task<int> CountActiveAsync()
            =>  await _dbSet.CountAsync(a => a.Status == SavingsAccountStatus.Active);

        public async Task<SavingsAccount?> GetByAccountNumberAsync(string accountNumber)
            => await _dbSet.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
    }
}
