using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class CreditCardRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly CreditCardRepository _repository;

        public CreditCardRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _repository = new CreditCardRepository(_dbContext);
        }

        private CreditCard BuildCreditCard(string cardNumber, string clientId, CreditCardStatus status = CreditCardStatus.Active, decimal currentDebt = 0m)
        {
            return new CreditCard
            {
                Id = 0,
                CardNumber = cardNumber,
                ClientId = clientId,
                CreditLimit = 5000m,
                CurrentDebt = currentDebt,
                ExpirationDate = "12/30",
                CvcHash = "hashed-cvc",
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task AddAsync_Should_Add_CreditCard_To_Database()
        {
            // Arrange
            var creditCard = BuildCreditCard("4000000000000001", "client-1");

            // Act
            var result = await _repository.AddAsync(creditCard);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var cards = await _dbContext.CreditCards.ToListAsync();
            cards.Should().ContainSingle();
        }

        [Fact]
        public async Task AddAsync_Should_Not_Persist_CreditCard_When_Not_Called()
        {
            // Arrange & Act
            var cards = await _dbContext.CreditCards.ToListAsync();

            // Assert
            cards.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_CreditCard_When_Exists()
        {
            // Arrange
            var creditCard = BuildCreditCard("4000000000000002", "client-2");
            _dbContext.CreditCards.Add(creditCard);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(creditCard.Id);

            // Assert
            result.Should().NotBeNull();
            result!.CardNumber.Should().Be("4000000000000002");
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
        public async Task UpdateAsync_Should_Modify_Existing_CreditCard()
        {
            // Arrange
            var creditCard = BuildCreditCard("4000000000000003", "client-3");
            _dbContext.CreditCards.Add(creditCard);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(creditCard).State = EntityState.Detached;

            var tracked = await _dbContext.CreditCards.FindAsync(creditCard.Id);
            tracked!.CurrentDebt = 1200m;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var updated = await _dbContext.CreditCards.AsNoTracking().FirstAsync(c => c.Id == creditCard.Id);
            updated.CurrentDebt.Should().Be(1200m);
        }

        [Fact]
        public async Task UpdateAsync_Should_Not_Modify_Other_CreditCards()
        {
            // Arrange
            var target = BuildCreditCard("4000000000000004", "client-4");
            var untouched = BuildCreditCard("4000000000000005", "client-4");
            _dbContext.CreditCards.AddRange(target, untouched);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(target).State = EntityState.Detached;
            _dbContext.Entry(untouched).State = EntityState.Detached;

            var tracked = await _dbContext.CreditCards.FindAsync(target.Id);
            tracked!.CurrentDebt = 2500m;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var stillZero = await _dbContext.CreditCards.AsNoTracking().FirstAsync(c => c.Id == untouched.Id);
            stillZero.CurrentDebt.Should().Be(0m);
        }

        [Fact]
        public async Task GetByCardNumberAsync_Should_Return_CreditCard_When_Exists()
        {
            // Arrange
            var creditCard = BuildCreditCard("4000000000000006", "client-5");
            _dbContext.CreditCards.Add(creditCard);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCardNumberAsync("4000000000000006");

            // Assert
            result.Should().NotBeNull();
            result!.ClientId.Should().Be("client-5");
        }

        [Fact]
        public async Task GetByCardNumberAsync_Should_Return_Null_When_NotExists()
        {
            // Act
            var result = await _repository.GetByCardNumberAsync("0000000000000000");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Return_Active_Cards_For_Client_Ordered_By_Most_Recent()
        {
            // Arrange
            var clientId = "client-6";
            var older = BuildCreditCard("4000000000000007", clientId);
            older.CreatedAt = DateTime.UtcNow.AddDays(-3);
            var newer = BuildCreditCard("4000000000000008", clientId);
            newer.CreatedAt = DateTime.UtcNow;
            _dbContext.CreditCards.AddRange(older, newer);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(newer.Id);
            result[1].Id.Should().Be(older.Id);
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Not_Include_Cancelled_Or_Other_Clients_Cards()
        {
            // Arrange
            var clientId = "client-7";
            var activeCard = BuildCreditCard("4000000000000009", clientId, CreditCardStatus.Active);
            var cancelledCard = BuildCreditCard("4000000000000010", clientId, CreditCardStatus.Cancelled);
            var otherClientCard = BuildCreditCard("4000000000000011", "other-client", CreditCardStatus.Active);
            _dbContext.CreditCards.AddRange(activeCard, cancelledCard, otherClientCard);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().ContainSingle();
            result.Should().NotContain(c => c.Id == cancelledCard.Id);
            result.Should().NotContain(c => c.Id == otherClientCard.Id);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
