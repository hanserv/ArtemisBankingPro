namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class ClientCreditCardPaymentDto
    {
        public required string SourceAccountNumber { get; set; }
        public required int CreditCardId { get; set; }
        public required decimal Amount { get; set; }
    }
}
