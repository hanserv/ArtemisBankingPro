namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class CreditCardDetailsViewModel
    {
        public required CreditCardViewModel Card { get; set; }
        public required List<CardConsumptionViewModel> Consumptions { get; set; }
    }
}
