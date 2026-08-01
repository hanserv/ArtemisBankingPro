using ArtemisBankingPro.Core.Application.DTOs.Email;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IEmailService
    {
        Task<Result> SendAsync(EmailRequestDto emailRequestDto);
    }
}
