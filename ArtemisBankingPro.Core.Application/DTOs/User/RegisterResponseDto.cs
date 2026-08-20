namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class RegisterResponseDto : BaseDto<string>
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Role { get; set; }
        public required bool IsActive { get; set; }
    }
}
