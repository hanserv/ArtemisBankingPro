namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class BeneficiaryTransactionConfirmationDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required string DestinationAccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
