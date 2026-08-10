using ArtemisBankingPro.Core.Domain.Common;
using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class CreditCard : BaseEntity<int>
    {
        public required string CardNumber { get; set; }
        public required string ClientId { get; set; }
        public required decimal CreditLimit { get; set; }
        public decimal CurrentDebt { get; set; } 
        public required string ExpirationDate { get; set; } 
        public required string CvcHash { get; set; } 

        public required string CreatedByAdminId { get; set; }
        public required CreditCardStatus Status { get; set; }
        public required DateTime CreatedAt { get; set; }

        public ICollection<CardConsumption>? Consumptions { get; set; }

        public decimal AvailableCredit => CreditLimit - CurrentDebt;
    }
}
