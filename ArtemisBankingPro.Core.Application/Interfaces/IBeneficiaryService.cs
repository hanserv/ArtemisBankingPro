using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IBeneficiaryService
    {
        Task<Result<List<BeneficiaryDto>>> GetByClientIdAsync(string clientId);
        Task<Result<BeneficiaryDto>> GetByIdAsync(int id, string clientId);
        Task<Result> AddAsync(AddBeneficiaryDto dto);
        Task<Result> DeleteAsync(int beneficiaryId, string clientId);
    }
}
