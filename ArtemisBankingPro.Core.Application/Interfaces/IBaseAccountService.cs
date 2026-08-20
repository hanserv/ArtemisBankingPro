using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IBaseAccountService
    {
        Task<Result> ChangeUserStatusAsync(string userId, bool isActive, string currentUserId);
        Task<Result> ConfirmAccountAsync(string userId, string token);
        Task<Result> EditUserAsync(UpdateUserDto updateDto, string currentUserId);
        Task<Result<PagedResult<UserDto>>> GetAllUsersAsync(UserFilterDto filter);
        Task<Result<UserDto>> GetUserByIdAsync(string userId);
        Task<Result<RegisterResponseDto>> RegisterUserAsync(RegisterDto registerDto, string role, string? origin = null, bool isApi = false);
        Task<Result> RequestPasswordResetAsync(RequestPasswordResetDto request, string? origin, bool isApi = false);
        Task<Result> ResetPasswordAsync(ResetPasswordDto request);
    }
}
