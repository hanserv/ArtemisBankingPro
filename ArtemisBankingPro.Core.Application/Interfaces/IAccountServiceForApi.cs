using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IAccountServiceForApi : IBaseAccountService
    {
        Task<LoginResponseForApiDto> AuthenticateAsync(LoginDto loginDto);
        Task ConfirmAccountApiAsync(ConfirmAccountDto dto);
        Task<PagedResult<UserCommerceDto>> GetCommerceUsersAsync(CommerceUserFilterDto filter);
        Task<UserDetailDto> GetUserDetailByIdAsync(string userId);
        Task<CommerceUserApiResponseDto> RegisterCommerceUserAsync(RegisterDto dto, int commerceId);
    }
}
