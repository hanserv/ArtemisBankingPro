namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class CommerceUserApiResponseDto : RegisterResponseDto
    {
        public required int CommerceId { get; set; }
    }
}
