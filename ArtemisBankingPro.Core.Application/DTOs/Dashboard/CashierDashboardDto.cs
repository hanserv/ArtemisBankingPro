namespace ArtemisBankingPro.Core.Application.DTOs.Dashboard
{
    public class CashierDashboardDto
    {
        public required int TodayTransactions { get; set; }
        public required int TodayPayments { get; set; }
        public required int TodayDeposits { get; set; }
        public required int TodayWithdrawals { get; set; }
    }
}
