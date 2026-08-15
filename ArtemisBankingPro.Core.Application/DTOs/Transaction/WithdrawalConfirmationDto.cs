namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class WithdrawalConfirmationDto
    {
        public required string AccountNumber { get; set; }
        public required string AccountHolderName { get; set; }
        public required decimal Amount { get; set; }
    }
}
