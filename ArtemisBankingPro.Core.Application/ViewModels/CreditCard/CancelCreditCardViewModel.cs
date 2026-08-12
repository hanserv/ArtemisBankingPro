namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class CancelCreditCardViewModel : BaseViewModel<int>
    {
        public required string LastFourDigits { get; set; }
    }
}
