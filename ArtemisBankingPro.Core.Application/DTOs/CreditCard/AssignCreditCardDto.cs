namespace ArtemisBankingPro.Core.Application.DTOs.CreditCard
{
    public class AssignCreditCardDto
    {
        public required string ClientId { get; set; }
        public required decimal CreditLimit { get; set; }
    }
}
