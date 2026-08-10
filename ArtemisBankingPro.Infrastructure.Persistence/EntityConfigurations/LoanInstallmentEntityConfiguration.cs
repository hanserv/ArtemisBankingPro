using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class LoanInstallmentEntityConfiguration : IEntityTypeConfiguration<LoanInstallment>
    {
        public void Configure(EntityTypeBuilder<LoanInstallment> builder)
        {
            #region Basic configuration
            builder.ToTable("LoanInstallments");
            builder.HasKey(i => i.Id);
            builder.HasIndex(i => new { i.LoanId, i.InstallmentNumber }).IsUnique();
            #endregion

            #region Property configurations
            builder.Property(i => i.LoanId).IsRequired();
            builder.Property(i => i.InstallmentNumber).IsRequired();
            builder.Property(i => i.DueDate).IsRequired();
            builder.Property(i => i.InstallmentAmount).IsRequired().HasPrecision(18, 2);
            builder.Property(i => i.InterestAmount).IsRequired().HasPrecision(18, 2);
            builder.Property(i => i.PrincipalAmount).IsRequired().HasPrecision(18, 2);
            builder.Property(i => i.RemainingBalance).IsRequired().HasPrecision(18, 2);
            builder.Property(i => i.Status).IsRequired();
            builder.Property(i => i.IsLate).IsRequired();
            #endregion

            #region Relationships
            #endregion
        }
    }
}
