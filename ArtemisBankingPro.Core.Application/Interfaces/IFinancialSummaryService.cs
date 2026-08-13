using ArtemisBankingPro.Core.Application.DTOs.Loan;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IFinancialSummaryService
    {
        Task<decimal> GetTotalDebtByClientAsync(string clientId);
        Task<decimal> GetSystemAverageDebtAsync();
        Task<LoanRiskWarningDto?> CheckIfHighRiskAsync(string clientId, decimal additionalDebt);
    }
}
