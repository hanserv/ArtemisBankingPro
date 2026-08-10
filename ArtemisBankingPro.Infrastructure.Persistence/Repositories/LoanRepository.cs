using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class LoanRepository : GenericRepository<Loan>, ILoanRepository
    {
        public LoanRepository(ArtemisBankingProContext context) : base(context)
        {
        }

        public async Task<bool> LoanNumberExistsAsync(string loanNumber)
            => await _dbSet.AnyAsync(l => l.LoanNumber == loanNumber);
    }
}
