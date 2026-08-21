using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class TransactionRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly TransactionRepository _repository;

        public TransactionRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _repository = new TransactionRepository(_dbContext);
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

        [Fact]
        public async Task AddAsync_Should_Add_Transaction_To_Database()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000101");
            var transaction = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 500m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-1",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.AddAsync(transaction);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var transactions = await _dbContext.Transactions.ToListAsync();
            transactions.Should().ContainSingle();
        }

        [Fact]
        public async Task AddAsync_Should_Not_Persist_Transaction_When_Not_Called()
        {
            // Arrange & Act
            var transactions = await _dbContext.Transactions.ToListAsync();

            // Assert
            transactions.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Transaction_When_Exists()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000102");
            var transaction = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 250m,
                Type = TransactionType.Debit,
                Category = TransactionCategory.Withdrawal,
                Origin = "ATM",
                Beneficiary = "client-2",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(transaction.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Category.Should().Be(TransactionCategory.Withdrawal);
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
        public async Task UpdateAsync_Should_Modify_Existing_Transaction()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000103");
            var transaction = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 100m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-3",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(transaction).State = EntityState.Detached;

            var tracked = await _dbContext.Transactions.FindAsync(transaction.Id);
            tracked!.Status = TransactionStatus.Rejected;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var updated = await _dbContext.Transactions.AsNoTracking().FirstAsync(t => t.Id == transaction.Id);
            updated.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task UpdateAsync_Should_Not_Modify_Other_Transactions()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000104");
            var target = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 100m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-4",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            var untouched = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 300m,
                Type = TransactionType.Debit,
                Category = TransactionCategory.Withdrawal,
                Origin = "ATM",
                Beneficiary = "client-4",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Transactions.AddRange(target, untouched);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(target).State = EntityState.Detached;
            _dbContext.Entry(untouched).State = EntityState.Detached;

            var tracked = await _dbContext.Transactions.FindAsync(target.Id);
            tracked!.Status = TransactionStatus.Rejected;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var stillApproved = await _dbContext.Transactions.AsNoTracking().FirstAsync(t => t.Id == untouched.Id);
            stillApproved.Status.Should().Be(TransactionStatus.Approved);
        }

        [Fact]
        public async Task GetByAccountIdAsync_Should_Return_Transactions_For_Account_Ordered_By_Most_Recent()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000105");
            var older = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 100m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-5",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            var newer = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 200m,
                Type = TransactionType.Debit,
                Category = TransactionCategory.Withdrawal,
                Origin = "ATM",
                Beneficiary = "client-5",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Transactions.AddRange(older, newer);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByAccountIdAsync(account.Id);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(newer.Id);
            result[1].Id.Should().Be(older.Id);
        }

        [Fact]
        public async Task GetByAccountIdAsync_Should_Not_Include_Transactions_From_Other_Accounts()
        {
            // Arrange
            var account = await CreateSavingsAccountAsync("100000106");
            var otherAccount = await CreateSavingsAccountAsync("100000107");

            var ownTransaction = new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 150m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-6",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            var otherTransaction = new Transaction
            {
                Id = 0,
                SavingsAccountId = otherAccount.Id,
                Amount = 999m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-7",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Transactions.AddRange(ownTransaction, otherTransaction);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByAccountIdAsync(account.Id);

            // Assert
            result.Should().ContainSingle();
            result.Should().NotContain(t => t.Id == otherTransaction.Id);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
