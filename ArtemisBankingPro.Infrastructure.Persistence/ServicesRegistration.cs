using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
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
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();

                services.AddSingleton(connection);

                services.AddDbContext<ArtemisBankingProContext>(opt => opt.UseSqlite(connection));
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
            services.AddScoped<IUnitOfWork,UnitOfWork>();
            services.AddScoped<ISavingsAccountRepository,SavingsAccountRepository>();
            services.AddScoped<ILoanRepository,LoanRepository>();
            services.AddScoped<ITransactionRepository,TransactionRepository>();
            services.AddScoped<ICreditCardRepository,CreditCardRepository>();
            services.AddScoped<IBeneficiaryRepository,BeneficiaryRepository>();
            services.AddScoped<ICardConsumptionRepository,CardConsumptionRepository>();
            services.AddScoped<ICommerceRepository,CommerceRepository>();
            #endregion
        }

        public static async Task RunDatabaseInitializationAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ArtemisBankingProContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                await context.Database.EnsureCreatedAsync(); // SQLite in-memory
            }
            else
            {
                await context.Database.MigrateAsync(); // SQL Server
            }
        }
    }
}
