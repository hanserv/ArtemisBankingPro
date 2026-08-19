using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IAccountServiceForApi : IBaseAccountService
    {
        Task<LoginResponseForApiDto> AuthenticateAsync(LoginDto loginDto);
        Task ConfirmAccountApiAsync(ConfirmAccountDto dto);
    }
}
