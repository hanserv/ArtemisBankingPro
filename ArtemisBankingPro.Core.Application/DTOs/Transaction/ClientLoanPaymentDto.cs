namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class ClientLoanPaymentDto
    {
        public required string SourceAccountNumber { get; set; }
        public required int LoanId { get; set; }
        public required decimal Amount { get; set; }
    }
}
