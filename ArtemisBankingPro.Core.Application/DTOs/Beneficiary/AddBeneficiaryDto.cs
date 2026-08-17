namespace ArtemisBankingPro.Core.Application.DTOs.Beneficiary
{
    public class AddBeneficiaryDto
    {
        public required string ClientId { get; set; }
        public required string AccountNumber { get; set; }
    }
}
