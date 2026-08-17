namespace ArtemisBankingPro.Core.Application.DTOs.Beneficiary
{
    public class BeneficiaryDto : BaseDto<int>
    {
        public required string FullName { get; set; }
        public required string AccountNumber { get; set; }
    }
}
