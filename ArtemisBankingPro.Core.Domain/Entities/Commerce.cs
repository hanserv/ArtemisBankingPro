using ArtemisBankingPro.Core.Domain.Common;

namespace ArtemisBankingPro.Core.Domain.Entities
{
    public class Commerce : BaseEntity<int>
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Rnc { get; set; } 
        public bool IsActive { get; set; } 
        public required DateTime CreatedAt { get; set; }

        public string? AssociatedUserId { get; set; }
        public required string CreatedByAdminId { get; set; }

        public ICollection<CardConsumption> Consumptions { get; set; } = [];
    }
}
