namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class BeneficiaryTransactionDto
    {
        public required string SourceAccountNumber { get; set; }
        public required int BeneficiaryId { get; set; }
        public required decimal Amount { get; set; }
    }
}
