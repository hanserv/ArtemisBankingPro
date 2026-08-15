namespace ArtemisBankingPro.Core.Application.ViewModels.Transaction
{
    public class LoanPaymentConfirmationViewModel
    {
        public required string SourceAccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required string LoanNumber { get; set; }
        public required string LoanHolderName { get; set; }
        public required decimal EnteredAmount { get; set; }
        public required decimal EffectiveAmount { get; set; }
    }
}
