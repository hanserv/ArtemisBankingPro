namespace ArtemisBankingPro.Core.Application.DTOs.HermesPay
{
    public class CommerceTransactionsResponseDto : PagedResult<CommerceTransactionDto>
    {
        public required int CommerceId { get; set; }
        public required string CommerceName { get; set; }
    }
}
