using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class CreditCardRepository : GenericRepository<CreditCard>, ICreditCardRepository
    {
        public CreditCardRepository(ArtemisBankingProContext context) : base(context)
        {
        }

        public async Task<bool> CardNumberExistsAsync(string cardNumber)
            => await _dbSet.AnyAsync(cc => cc.CardNumber == cardNumber);

        public async Task<int> CountActiveAsync()
            => await _dbSet.CountAsync(a => a.Status == CreditCardStatus.Active);
    }
}
