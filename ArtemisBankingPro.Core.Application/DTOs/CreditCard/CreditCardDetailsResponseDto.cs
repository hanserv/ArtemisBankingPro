namespace ArtemisBankingPro.Core.Application.DTOs.CreditCard
{
    public class CreditCardDetailsResponseDto : CreditCardDto
    {
        public required List<CardConsumptionDto> Consumptions { get; set; }
    }
}
