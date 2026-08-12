using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.DTOs.CreditCard
{
    public class CreditCardFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public CreditCardStatus? Status { get; set; }
        public string? Identification { get; set; }
    }
}
