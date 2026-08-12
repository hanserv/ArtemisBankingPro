namespace ArtemisBankingPro.Core.Application.DTOs.CreditCard
{
    public class ModifyCreditCardLimitDto
    {
        public required int CreditCardId { get; set; }
        public required decimal CreditLimit { get; set; }
    }
}
