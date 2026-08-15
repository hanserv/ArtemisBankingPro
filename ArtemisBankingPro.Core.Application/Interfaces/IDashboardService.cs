using ArtemisBankingPro.Core.Application.DTOs.Dashboard;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<Result<AdminDashboardDto>> GetAdminSummaryAsync();
        Task<Result<CashierDashboardDto>> GetCashierSummaryAsync(string cashierId);
    }
}
