using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class BeneficiaryRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly BeneficiaryRepository _repository;

        public BeneficiaryRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _repository = new BeneficiaryRepository(_dbContext);
        }

        private async Task<SavingsAccount> CreateSavingsAccountAsync(string accountNumber, string clientId = "client-x")
        {
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = 1000m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

        private static Beneficiary BuildBeneficiary(string clientId, int savingsAccountId)
        {
            return new Beneficiary
            {
                Id = 0,
                ClientId = clientId,
                SavingsAccountId = savingsAccountId,
                CreatedAt = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task AddAsync_Should_Add_Beneficiary_To_Database()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("200000101");
            var beneficiary = BuildBeneficiary("client-1", account.Id);

            // Act
            var result = await _repository.AddAsync(beneficiary);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var beneficiaries = await _dbContext.Beneficiaries.ToListAsync();
            beneficiaries.Should().ContainSingle();
        }

        [Fact]
        public async Task AddAsync_Should_Not_Persist_Beneficiary_When_Not_Called()
        {
            // Arrange & Act
            var beneficiaries = await _dbContext.Beneficiaries.ToListAsync();

            // Assert
            beneficiaries.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Beneficiary_When_Exists()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("200000102");
            var beneficiary = BuildBeneficiary("client-2", account.Id);
            _dbContext.Beneficiaries.Add(beneficiary);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(beneficiary.Id);

            // Assert
            result.Should().NotBeNull();
            result!.ClientId.Should().Be("client-2");
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_When_NotExists()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_Should_Modify_Existing_Beneficiary()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("200000103");
            var otherAccount = await CreateSavingsAccountAsync("200000104");
            var beneficiary = BuildBeneficiary("client-3", account.Id);
            _dbContext.Beneficiaries.Add(beneficiary);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(beneficiary).State = EntityState.Detached;

            var tracked = await _dbContext.Beneficiaries.FindAsync(beneficiary.Id);
            tracked!.SavingsAccountId = otherAccount.Id;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var updated = await _dbContext.Beneficiaries.AsNoTracking().FirstAsync(b => b.Id == beneficiary.Id);
            updated.SavingsAccountId.Should().Be(otherAccount.Id);
        }

        [Fact]
        public async Task UpdateAsync_Should_Not_Modify_Other_Beneficiaries()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("200000105");
            var otherAccount = await CreateSavingsAccountAsync("200000106");
            var thirdAccount = await CreateSavingsAccountAsync("200000112");

            var target = BuildBeneficiary("client-4", account.Id);
            var untouched = BuildBeneficiary("client-4", thirdAccount.Id); // <- distinto SavingsAccountId
            _dbContext.Beneficiaries.AddRange(target, untouched);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(target).State = EntityState.Detached;
            _dbContext.Entry(untouched).State = EntityState.Detached;

            var tracked = await _dbContext.Beneficiaries.FindAsync(target.Id);
            tracked!.SavingsAccountId = otherAccount.Id;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var stillOriginal = await _dbContext.Beneficiaries.AsNoTracking().FirstAsync(b => b.Id == untouched.Id);
            stillOriginal.SavingsAccountId.Should().Be(thirdAccount.Id);
        }

        [Fact]
        public async Task GetByClientIdAsync_Should_Return_Beneficiaries_For_Client_Ordered_By_Most_Recent_With_SavingsAccount_Included()
        {
            // Arrange
            var clientId = "client-5";
            var account1 = await CreateSavingsAccountAsync("200000107");
            var account2 = await CreateSavingsAccountAsync("200000108");

            var older = BuildBeneficiary(clientId, account1.Id);
            older.CreatedAt = DateTime.UtcNow.AddDays(-3);
            var newer = BuildBeneficiary(clientId, account2.Id);
            newer.CreatedAt = DateTime.UtcNow;
            _dbContext.Beneficiaries.AddRange(older, newer);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByClientIdAsync(clientId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(newer.Id);
            result[1].Id.Should().Be(older.Id);
            result[0].SavingsAccount.Should().NotBeNull();
            result[0].SavingsAccount!.AccountNumber.Should().Be("200000108");
        }

        [Fact]
        public async Task GetByClientIdAsync_Should_Not_Include_Other_Clients_Beneficiaries()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("200000109");
            var ownBeneficiary = BuildBeneficiary("client-6", account.Id);
            var otherBeneficiary = BuildBeneficiary("other-client", account.Id);
            _dbContext.Beneficiaries.AddRange(ownBeneficiary, otherBeneficiary);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByClientIdAsync("client-6");

            // Assert
            result.Should().ContainSingle();
            result.Should().NotContain(b => b.Id == otherBeneficiary.Id);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
