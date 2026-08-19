namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class ExpressTransactionConfirmationViewModel
    {
        public required string SourceAccountNumber { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required string DestinationAccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
