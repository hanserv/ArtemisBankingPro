namespace ArtemisBankingPro.Core.Application.DTOs.Transaction
{
    public class OwnAccountTransferConfirmationDto
    {
        public required string SourceAccountNumber { get; set; }
        public required string DestinationAccountNumber { get; set; }
        public required decimal Amount { get; set; }
    }
}
