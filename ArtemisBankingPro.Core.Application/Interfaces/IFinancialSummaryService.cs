namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IFinancialSummaryService
    {
        Task<decimal> GetTotalDebtByClientAsync(string clientId);
        Task<decimal> GetSystemAverageDebtAsync();
        Task<bool> CheckIfHighRiskAsync(string clientId, decimal additionalDebt);
    }
}
