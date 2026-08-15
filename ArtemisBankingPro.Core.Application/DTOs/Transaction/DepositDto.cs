namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class DepositDto
    {
        public required string AccountNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
