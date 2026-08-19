namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class CashAdvanceDto
    {
        public required int CreditCardId { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
