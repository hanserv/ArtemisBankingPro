namespace ArtemisBankingPro.Core.Application.DTOs.Commerce
{
    public class CommerceDetailsDto : BaseDto<int>
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Rnc { get; set; }
        public required bool IsActive { get; set; }
        public required DateTime CreatedAt { get; set; }
        public CommerceAssociatedUserDto? AssociatedUser { get; set; }
    }
}
