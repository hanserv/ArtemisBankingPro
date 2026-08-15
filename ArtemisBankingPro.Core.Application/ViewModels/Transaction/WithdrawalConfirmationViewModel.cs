namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class WithdrawalConfirmationViewModel
    {
        public required string AccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
