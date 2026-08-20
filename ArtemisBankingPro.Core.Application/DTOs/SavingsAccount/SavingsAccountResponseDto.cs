using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.SavingsAccount
{
    public class SavingsAccountResponseDto : BaseDto<int>
    {
        public required string AccountNumber { get; set; }
        public required string ClientId { get; set; }
        public required string ClientFullName { get; set; }
        public required string Identification { get; set; }
        public required decimal Balance { get; set; }
        public required SavingsAccountType Type { get; set; }
        public required SavingsAccountStatus Status { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
