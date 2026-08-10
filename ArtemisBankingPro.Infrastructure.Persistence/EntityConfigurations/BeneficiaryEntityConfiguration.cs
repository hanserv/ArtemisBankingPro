using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class BeneficiaryEntityConfiguration : IEntityTypeConfiguration<Beneficiary>
    {
        public void Configure(EntityTypeBuilder<Beneficiary> builder)
        {
            #region Basic configuration
            builder.ToTable("Beneficiaries");
            builder.HasKey(b => b.Id);
            builder.HasIndex(b => new { b.ClientId, b.SavingsAccountId }).IsUnique();
            #endregion

            #region Property configurations
            builder.Property(b => b.ClientId).IsRequired();
            builder.Property(b => b.SavingsAccountId).IsRequired();
            builder.Property(b => b.CreatedAt).IsRequired();
            #endregion

            #region Relationships
            #endregion
        }
    }
}
