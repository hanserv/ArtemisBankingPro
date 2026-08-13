using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.Loan
{
    public class LoanRiskWarningDto
    {
        public required RiskType RiskType { get; set; }
        public required decimal CurrentDebt { get; set; }
        public required decimal ProjectedDebt { get; set; }
        public required decimal AverageDebt { get; set; }
    }
}
