using Microsoft.AspNetCore.Identity;

namespace ArtemisBankingPro.Infrastructure.Identity.Entities
{
    public class AppUser : IdentityUser
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Identification { get; set; }
        public required bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CommerceId { get; set; }
    }
}
