using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IBasicUserInfoService
    {
        Task<string?> GetUserIdByIdentificationAsync(string identification);
        Task<string> GetFullNameAsync(string userId);
        Task<List<UserBasicInfoDto>> GetActiveClientsAsync(string? identification);
        Task<bool?> IsClientActiveAsync(string clientId);
        Task<UserBasicInfoDto?> GetBasicInfoAsync(string userId);
        Task<(int Active, int Inactive)> GetClientStatusCountsAsync();
        Task<HashSet<int>> GetCommerceIdsWithAssociatedUserAsync(IEnumerable<int> commerceIds);
        Task<CommerceAssociatedUserDto?> GetCommerceAssociatedUserInfoAsync(string userId);
        Task DeactivateUserAsync(string userId);
    }
}
