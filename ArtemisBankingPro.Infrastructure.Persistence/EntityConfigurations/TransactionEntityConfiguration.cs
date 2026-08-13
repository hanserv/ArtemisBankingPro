using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            #region Basic configuration
            builder.ToTable("Transactions");
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => t.CreatedAt);
            #endregion

            #region Property configurations
            builder.Property(t => t.SavingsAccountId).IsRequired();
            builder.Property(t => t.Amount).IsRequired().HasPrecision(18, 2);
            builder.Property(t => t.Type).IsRequired();
            builder.Property(t => t.Category).IsRequired();
            builder.Property(t => t.Origin).IsRequired().HasMaxLength(50);
            builder.Property(t => t.Beneficiary).IsRequired().HasMaxLength(50);
            builder.Property(t => t.Status).IsRequired();
            builder.Property(t => t.PerformedByUserId);
            builder.Property(t => t.CreatedAt).IsRequired();
            #endregion

            #region Relationships
            #endregion
        }
    }
}
