using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ILoanRepository : IGenericRepository<Loan>
    {
        Task<bool> LoanNumberExistsAsync(string loanNumber);
    }
}
