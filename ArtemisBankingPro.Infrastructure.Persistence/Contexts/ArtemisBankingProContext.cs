using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Contexts
{
    public class ArtemisBankingProContext : DbContext
    {
        public ArtemisBankingProContext(DbContextOptions<ArtemisBankingProContext> options) : base(options) { }

        // db sets

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
