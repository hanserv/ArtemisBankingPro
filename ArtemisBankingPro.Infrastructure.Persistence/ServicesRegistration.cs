using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Infrastructure.Persistence
{
    public static class ServicesRegistration
    {
        public static void AddPersistenceLayer(this IServiceCollection services, IConfiguration configuration)
        {
            #region Contexts
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                services.AddDbContext<ArtemisBankingProContext>(opt => opt.UseInMemoryDatabase("AppDb"));
            }
            else
            {
                services.AddDbContext<ArtemisBankingProContext>(opt =>
                    opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                        m => m.MigrationsAssembly(typeof(ArtemisBankingProContext).Assembly.FullName)));
            }
            #endregion

            #region Repositories
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            #endregion
        }
    }
}
