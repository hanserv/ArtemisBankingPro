using System.Net;

namespace ArtemisBankingPro.Core.Application.Exceptions
{
    public class HighRiskLoanException : ApiException
    {
        public string RiskType { get; }
        public decimal CurrentDebt { get; }
        public decimal ProjectedDebt { get; }
        public decimal AverageDebt { get; }

        public HighRiskLoanException(string message, string riskType, decimal currentDebt, decimal projectedDebt, decimal averageDebt)
            : base(message, (int)HttpStatusCode.Conflict)
        {
            RiskType = riskType;
            CurrentDebt = currentDebt;
            ProjectedDebt = projectedDebt;
            AverageDebt = averageDebt;
        }
    }
}
