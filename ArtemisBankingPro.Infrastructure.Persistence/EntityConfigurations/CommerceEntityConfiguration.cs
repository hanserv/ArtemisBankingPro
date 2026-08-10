using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class CommerceEntityConfiguration : IEntityTypeConfiguration<Commerce>
    {
        public void Configure(EntityTypeBuilder<Commerce> builder)
        {
            #region Basic configuration
            builder.ToTable("Commerces");
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.Rnc).IsUnique();
            builder.HasIndex(c => c.Email).IsUnique();
            builder.HasIndex(c => c.AssociatedUserId).IsUnique();
            #endregion

            #region Property configurations
            builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Description).HasMaxLength(500);
            builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
            builder.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
            builder.Property(c => c.Rnc).IsRequired().HasMaxLength(11);
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.AssociatedUserId);
            builder.Property(c => c.CreatedByAdminId).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();
            #endregion

            #region Relationships
            builder.HasMany(c => c.Consumptions)
                   .WithOne(cc => cc.Commerce)
                   .HasForeignKey(cc => cc.CommerceId)
                   .OnDelete(DeleteBehavior.Restrict);
            #endregion
        }
    }
}
