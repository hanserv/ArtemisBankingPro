namespace ArtemisBankingPro.Core.Application.ViewModels.Dashboard
{
    public class AdminDashboardViewModel
    {
        public required int TotalTransactions { get; set; }
        public required int TodayTransactions { get; set; }
        public required int TotalPayments { get; set; }
        public required int TodayPayments { get; set; }
        public required int ActiveClients { get; set; }
        public required int InactiveClients { get; set; }
        public required int TotalFinancialProducts { get; set; }
        public required int ActiveLoans { get; set; }
        public required int ActiveCreditCards { get; set; }
        public required int ActiveSavingsAccounts { get; set; }
        public required decimal AverageDebtPerClient { get; set; }
    }
}
