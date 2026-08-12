using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IAccountServiceForWebApp : IBaseAccountService
    {
        Task<Result<LoginResponseDto>> AuthenticateAsync(LoginDto loginDto);
        Task<Result<List<ClientForAssignmentDto>>> GetClientsForAssignmentAsync(string? identification);
        Task SignOutAsync();
    }
}
