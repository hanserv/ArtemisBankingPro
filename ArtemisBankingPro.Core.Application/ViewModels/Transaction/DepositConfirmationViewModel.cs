namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class DepositConfirmationViewModel
    {
        public required string AccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
