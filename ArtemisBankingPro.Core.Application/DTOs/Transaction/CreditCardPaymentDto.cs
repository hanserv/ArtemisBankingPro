namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class CreditCardPaymentDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string CardNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
