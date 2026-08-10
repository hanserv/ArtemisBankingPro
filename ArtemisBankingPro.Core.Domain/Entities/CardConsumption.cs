using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class CardConsumption : BaseEntity<int>
    {
        public required int CreditCardId { get; set; }
        public CreditCard? CreditCard { get; set; }

        public int? CommerceId { get; set; }
        public Commerce? Commerce { get; set; }

        public required decimal Amount { get; set; }
        public required ConsumptionStatus Status { get; set; }
        public required DateTime ConsumptionDate { get; set; }
    }
}
