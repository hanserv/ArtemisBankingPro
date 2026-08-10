using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class SavingsAccountEntityConfiguration : IEntityTypeConfiguration<SavingsAccount>
    {
        public void Configure(EntityTypeBuilder<SavingsAccount> builder)
        {
            #region Basic configuration
            builder.ToTable("SavingsAccounts");
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => a.AccountNumber).IsUnique();
            builder.HasIndex(a => a.ClientId);
            #endregion

            #region Property configurations
            builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(9);
            builder.Property(a => a.ClientId).IsRequired(); 
            builder.Property(a => a.Balance).IsRequired().HasPrecision(18, 2);
            builder.Property(a => a.Type).IsRequired();
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();
            #endregion

            #region Relationships
            builder.HasMany(a => a.Transactions)
                    .WithOne(t => t.SavingsAccount)
                    .HasForeignKey(t => t.SavingsAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Beneficiaries)
                   .WithOne(b => b.SavingsAccount)
                   .HasForeignKey(b => b.SavingsAccountId)
                   .OnDelete(DeleteBehavior.Restrict);
            #endregion
        }
    }
}
