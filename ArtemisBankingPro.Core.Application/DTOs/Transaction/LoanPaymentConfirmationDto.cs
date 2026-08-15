namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class LoanPaymentConfirmationDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required string LoanNumber { get; set; }
        public required string LoanHolderName { get; set; }
        public required decimal EnteredAmount { get; set; }
        public required decimal EffectiveAmount { get; set; }
    }
}
