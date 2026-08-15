namespace ArtemisBankingPro.Core.Application.ViewModels.Dashboard
{
    public class CashierDashboardViewModel
    {
        public required int TodayTransactions { get; set; }
        public required int TodayPayments { get; set; }
        public required int TodayDeposits { get; set; }
        public required int TodayWithdrawals { get; set; }
    }
}
