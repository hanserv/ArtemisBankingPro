namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class OwnAccountTransferConfirmationViewModel
    {
        public required string SourceAccountNumber { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
