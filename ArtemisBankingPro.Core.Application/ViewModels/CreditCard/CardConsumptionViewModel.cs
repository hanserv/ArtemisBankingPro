using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class CardConsumptionViewModel
    {
        public required DateTime Date { get; set; }
        public required decimal Amount { get; set; }
        public required string CommerceName { get; set; }
        public required ConsumptionStatus Status { get; set; }
    }
}
