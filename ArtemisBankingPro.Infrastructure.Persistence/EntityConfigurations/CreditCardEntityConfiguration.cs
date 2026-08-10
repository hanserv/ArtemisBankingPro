using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class CreditCardEntityConfiguration : IEntityTypeConfiguration<CreditCard>
    {
        public void Configure(EntityTypeBuilder<CreditCard> builder)
        {
            #region Basic configuration
            builder.ToTable("CreditCards");
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.CardNumber).IsUnique();
            builder.HasIndex(c => c.ClientId);
            #endregion

            #region Property configurations
            builder.Property(c => c.CardNumber).IsRequired().HasMaxLength(16);
            builder.Property(c => c.ClientId).IsRequired();
            builder.Property(c => c.CreditLimit).IsRequired().HasPrecision(18, 2);
            builder.Property(c => c.CurrentDebt).IsRequired().HasPrecision(18, 2);
            builder.Property(c => c.ExpirationDate).IsRequired().HasMaxLength(5);
            builder.Property(c => c.CvcHash).IsRequired();
            builder.Property(c => c.CreatedByAdminId).IsRequired();
            builder.Property(c => c.Status).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();

            builder.Ignore(c => c.AvailableCredit);
            #endregion

            #region Relationships
            builder.HasMany(c => c.Consumptions)
                   .WithOne(cc => cc.CreditCard)
                   .HasForeignKey(cc => cc.CreditCardId)
                   .OnDelete(DeleteBehavior.Restrict);
            #endregion
        }
    }
}
