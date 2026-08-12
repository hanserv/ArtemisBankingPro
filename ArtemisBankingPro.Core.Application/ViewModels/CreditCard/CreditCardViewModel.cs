using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class CreditCardViewModel : BaseViewModel<int>
    {
        public required string LastFourDigits { get; set; }
        public required string ClientFullName { get; set; }
        public required decimal CreditLimit { get; set; }
        public required decimal CurrentDebt { get; set; }
        public required decimal AvailableCredit { get; set; }
        public required string ExpirationDate { get; set; }
        public required CreditCardStatus Status { get; set; }
        public required string CreatedByAdminName { get; set; }
    }
}
