namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class CancelSavingsAccountViewModel : BaseViewModel<int>
    {
        public required string AccountNumber { get; set; }
    }
}
