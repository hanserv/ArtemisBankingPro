using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class LoanRiskWarningViewModel
    {
        public required AssignLoanViewModel Loan { get; set; }
        public required RiskType RiskType { get; set; }
        public required decimal CurrentDebt { get; set; }
        public required decimal ProjectedDebt { get; set; }
        public required decimal AverageDebt { get; set; }
    }
}
