using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class LoanEntityConfiguration : IEntityTypeConfiguration<Loan>
    {
        public void Configure(EntityTypeBuilder<Loan> builder)
        {
            #region Basic configuration
            builder.ToTable("Loans");
            builder.HasKey(l => l.Id);
            builder.HasIndex(l => l.LoanNumber).IsUnique();
            builder.HasIndex(l => l.ClientId);
            #endregion

            #region Property configurations
            builder.Property(l => l.LoanNumber).IsRequired().HasMaxLength(9);
            builder.Property(l => l.ClientId).IsRequired().HasMaxLength(450);
            builder.Property(l => l.CapitalAmount).IsRequired().HasPrecision(18, 2);
            builder.Property(l => l.PendingAmount).IsRequired().HasPrecision(18, 2);
            builder.Property(l => l.AnnualInterestRate).IsRequired().HasPrecision(5, 2);
            builder.Property(l => l.TermInMonths).IsRequired();
            builder.Property(l => l.CreatedByAdminId).IsRequired();
            builder.Property(l => l.Status).IsRequired();
            builder.Property(l => l.CreatedAt).IsRequired();
            #endregion

            #region Relationships
            builder.HasMany(l => l.Installments)
                   .WithOne(i => i.Loan)
                   .HasForeignKey(i => i.LoanId)
                   .OnDelete(DeleteBehavior.Restrict);
            #endregion
        }
    }
}
