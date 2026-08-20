using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.HermesPay
{
    public class CommerceTransactionDto : BaseDto<int>
    {
        public required DateTime TransactionDate { get; set; }
        public required decimal Amount { get; set; }
        public required string CardLastFourDigits { get; set; }
        public required TransactionStatus Status { get; set; }
    }
}
