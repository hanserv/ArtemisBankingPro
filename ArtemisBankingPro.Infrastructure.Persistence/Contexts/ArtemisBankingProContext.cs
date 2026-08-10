using System.Reflection;
using ArtemisBankingPro.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Persistence.Contexts
{
    public class ArtemisBankingProContext : DbContext
    {
        public ArtemisBankingProContext(DbContextOptions<ArtemisBankingProContext> options) : base(options) { }

        public DbSet<SavingsAccount> SavingsAccounts { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<LoanInstallment> LoanInstallments { get; set; }
        public DbSet<CreditCard> CreditCards { get; set; }
        public DbSet<CardConsumption> CardConsumptions { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<Commerce> Commerces { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
