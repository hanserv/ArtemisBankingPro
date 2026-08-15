namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class LoanPaymentDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string LoanNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
