namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ThirdPartyTransactionConfirmationViewModel
    {
        public required string SourceAccountNumber { get; set; }
        public required string SourceAccountHolderName { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required string DestinationAccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
