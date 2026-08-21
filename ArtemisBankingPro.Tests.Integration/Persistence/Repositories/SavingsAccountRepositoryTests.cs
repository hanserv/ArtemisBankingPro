using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class SavingsAccountRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly SavingsAccountRepository _repository;

        public SavingsAccountRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated(); 

            _repository = new SavingsAccountRepository(_dbContext);
        }

        [Fact]
        public async Task AddAsync_Should_Add_SavingsAccount_To_Database()
        {
            // Arrange
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000001",
                ClientId = "client-1",
                Balance = 1000m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.AddAsync(account);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var accounts = await _dbContext.SavingsAccounts.ToListAsync();
            accounts.Should().ContainSingle();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_SavingsAccount_When_Exists()
        {
            // Arrange
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000002",
                ClientId = "client-2",
                Balance = 500m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(account.Id);

            // Assert
            result.Should().NotBeNull();
            result!.AccountNumber.Should().Be("100000002");
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
        public async Task UpdateAsync_Should_Modify_Existing_SavingsAccount()
        {
            // Arrange
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000003",
                ClientId = "client-3",
                Balance = 200m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;

            var trackedAccount = await _dbContext.SavingsAccounts.FindAsync(account.Id);
            trackedAccount!.Balance = 750m;
            _dbContext.Entry(trackedAccount).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(trackedAccount);

            // Assert
            var updated = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == account.Id);
            updated.Balance.Should().Be(750m);
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Not_Include_Inactive_Or_Other_Client_Accounts()
        {
            // Arrange
            var clientId = "client-12";
            var inactiveAccount = new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000017",
                ClientId = clientId,
                Balance = 100m,
                Type = SavingsAccountType.Secondary,
                Status = SavingsAccountStatus.Cancelled,
                CreatedAt = DateTime.UtcNow
            };
            var otherClientAccount = new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000018",
                ClientId = "other-client-2",
                Balance = 200m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.AddRange(inactiveAccount, otherClientAccount);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().NotContain(a => a.AccountNumber == "100000017");
            result.Should().NotContain(a => a.AccountNumber == "100000018");
        }

        [Fact]
        public async Task AccountNumberExistsAsync_Should_Return_True_When_AccountNumber_Exists()
        {
            // Arrange
            _dbContext.SavingsAccounts.Add(new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000004",
                ClientId = "client-4",
                Balance = 100m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.AccountNumberExistsAsync("100000004");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task AccountNumberExistsAsync_Should_Return_False_When_AccountNumber_NotExists()
        {
            // Act
            var result = await _repository.AccountNumberExistsAsync("999999999");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetPrincipalAccountByClientIdAsync_Should_Return_PrincipalAccount()
        {
            // Arrange
            var clientId = "client-5";
            _dbContext.SavingsAccounts.AddRange(
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000005",
                    ClientId = clientId,
                    Balance = 300m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000006",
                    ClientId = clientId,
                    Balance = 150m,
                    Type = SavingsAccountType.Secondary,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetPrincipalAccountByClientIdAsync(clientId);

            // Assert
            result.Should().NotBeNull();
            result!.Type.Should().Be(SavingsAccountType.Principal);
        }

        [Fact]
        public async Task GetPrincipalAccountByClientIdAsync_Should_Return_Null_When_NotExists()
        {
            // Act
            var result = await _repository.GetPrincipalAccountByClientIdAsync("nonexistent-client");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CountActiveAsync_Should_Return_Count_Of_Active_Accounts_Only()
        {
            // Arrange
            _dbContext.SavingsAccounts.AddRange(
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000007",
                    ClientId = "client-6",
                    Balance = 100m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000008",
                    ClientId = "client-7",
                    Balance = 100m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000009",
                    ClientId = "client-8",
                    Balance = 100m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Cancelled,
                    CreatedAt = DateTime.UtcNow
                });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.CountActiveAsync();

            // Assert
            result.Should().Be(2);
        }

        [Fact]
        public async Task GetByAccountNumberAsync_Should_Return_SavingsAccount_When_Exists()
        {
            // Arrange
            _dbContext.SavingsAccounts.Add(new SavingsAccount
            {
                Id = 0,
                AccountNumber = "100000010",
                ClientId = "client-9",
                Balance = 400m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByAccountNumberAsync("100000010");

            // Assert
            result.Should().NotBeNull();
            result!.ClientId.Should().Be("client-9");
        }

        [Fact]
        public async Task GetByAccountNumberAsync_Should_Return_Null_When_NotExists()
        {
            // Act
            var result = await _repository.GetByAccountNumberAsync("000000000");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Return_Only_Active_Accounts_For_Client()
        {
            // Arrange
            var clientId = "client-10";
            _dbContext.SavingsAccounts.AddRange(
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000011",
                    ClientId = clientId,
                    Balance = 100m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000012",
                    ClientId = clientId,
                    Balance = 200m,
                    Type = SavingsAccountType.Secondary,
                    Status = SavingsAccountStatus.Cancelled,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000013",
                    ClientId = "other-client",
                    Balance = 300m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().ContainSingle();
            result[0].AccountNumber.Should().Be("100000011");
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Order_Principal_First_Then_By_Balance_Descending()
        {
            // Arrange
            var clientId = "client-11";
            _dbContext.SavingsAccounts.AddRange(
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000014",
                    ClientId = clientId,
                    Balance = 900m,
                    Type = SavingsAccountType.Secondary,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000015",
                    ClientId = clientId,
                    Balance = 100m,
                    Type = SavingsAccountType.Principal,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new SavingsAccount
                {
                    Id = 0,
                    AccountNumber = "100000016",
                    ClientId = clientId,
                    Balance = 500m,
                    Type = SavingsAccountType.Secondary,
                    Status = SavingsAccountStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().HaveCount(3);
            result[0].AccountNumber.Should().Be("100000015");
            result[1].AccountNumber.Should().Be("100000014"); 
            result[2].AccountNumber.Should().Be("100000016"); 
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Return_Empty_When_No_Active_Accounts()
        {
            // Act
            var result = await _repository.GetActiveByClientIdAsync("nonexistent-client");

            // Assert
            result.Should().BeEmpty();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
