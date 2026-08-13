using ArtemisBankingPro.Core.Domain.Common.Enums;
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

        public async Task<bool> ClientHasActiveLoanAsync(string clientId)
            => await _dbSet.AnyAsync(l => l.ClientId == clientId && l.Status == LoanStatus.Active);

        public async Task<int> MarkOverdueInstallmentsAsync()
        {
            var today = DateTime.UtcNow.Date;

            return await _context.LoanInstallments
                .Where(i => i.DueDate.Date < today && i.Status != InstallmentStatus.Paid && !i.IsLate)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.IsLate, true));
        }
    }
}
