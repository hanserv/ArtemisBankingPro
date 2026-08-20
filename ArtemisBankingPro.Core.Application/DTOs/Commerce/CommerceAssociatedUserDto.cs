namespace ArtemisBankingPro.Core.Application.DTOs.Commerce
{
    public class CommerceAssociatedUserDto : BaseDto<string>
    {
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required bool IsActive { get; set; }
    }
}
