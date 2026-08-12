using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.CreditCard
{
    public class CreditCardDto : BaseDto<int>
    {
        public required string LastFourDigits { get; set; }
        public required string ClientId { get; set; }
        public required string ClientFullName { get; set; }
        public required decimal CreditLimit { get; set; }
        public required decimal CurrentDebt { get; set; }
        public required decimal AvailableCredit { get; set; }
        public required string ExpirationDate { get; set; }
        public required CreditCardStatus Status { get; set; }
        public required string CreatedByAdminName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
