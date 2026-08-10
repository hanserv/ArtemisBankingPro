namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class UserDto : BaseDto<string>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Identification { get; set; }
        public required string Role { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
