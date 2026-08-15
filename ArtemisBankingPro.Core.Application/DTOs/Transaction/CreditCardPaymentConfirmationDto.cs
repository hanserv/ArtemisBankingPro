namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class CreditCardPaymentConfirmationDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required string CardNumber { get; set; }
        public required string CardLastFourDigits { get; set; }
        public required string CardHolderName { get; set; }
        public required decimal EnteredAmount { get; set; }
        public required decimal EffectiveAmount { get; set; }
    }
}
