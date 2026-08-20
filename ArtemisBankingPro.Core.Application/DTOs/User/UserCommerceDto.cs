namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class UserCommerceDto : UserDto
    {
        public int? CommerceId { get; set; }
        public string? CommerceName { get; set; }
    }
}
