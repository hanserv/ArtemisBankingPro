namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class WithdrawalDto
    {
        public required string AccountNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
