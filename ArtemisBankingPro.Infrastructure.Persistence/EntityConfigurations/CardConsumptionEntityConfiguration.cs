using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtemisBankingPro.Infrastructure.Persistence.EntityConfigurations
{
    public class CardConsumptionEntityConfiguration : IEntityTypeConfiguration<CardConsumption>
    {
        public void Configure(EntityTypeBuilder<CardConsumption> builder)
        {
            #region Basic configuration
            builder.ToTable("CardConsumptions");
            builder.HasKey(c => c.Id);
            #endregion

            #region Property configurations
            builder.Property(c => c.CreditCardId).IsRequired();
            builder.Property(c => c.CommerceId); 
            builder.Property(c => c.Amount).IsRequired().HasPrecision(18, 2);
            builder.Property(c => c.Status).IsRequired();
            builder.Property(c => c.ConsumptionDate).IsRequired();
            #endregion

            #region Relationships
            #endregion
        }
    }
}
