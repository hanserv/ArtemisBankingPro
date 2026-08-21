using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class CommerceRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly CommerceRepository _repository;

        public CommerceRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _repository = new CommerceRepository(_dbContext);
        }

        private static Commerce BuildCommerce(string name, string rnc, bool isActive = true)
        {
            return new Commerce
            {
                Id = 0,
                Name = name,
                Description = "Test commerce",
                Email = $"{name.ToLower()}@commerce.com",
                PhoneNumber = "8095551234",
                Rnc = rnc,
                IsActive = isActive,
                CreatedByAdminId = "admin-1",
                CreatedAt = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task AddAsync_Should_Add_Commerce_To_Database()
        {
            // Arrange
            var commerce = BuildCommerce("Commerce One", "101000001");

            // Act
            var result = await _repository.AddAsync(commerce);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var commerces = await _dbContext.Commerces.ToListAsync();
            commerces.Should().ContainSingle();
        }

        [Fact]
        public async Task AddAsync_Should_Not_Persist_Commerce_When_Not_Called()
        {
            // Arrange & Act
            var commerces = await _dbContext.Commerces.ToListAsync();

            // Assert
            commerces.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Commerce_When_Exists()
        {
            // Arrange
            var commerce = BuildCommerce("Commerce Two", "101000002");
            _dbContext.Commerces.Add(commerce);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(commerce.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Commerce Two");
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
        public async Task UpdateAsync_Should_Modify_Existing_Commerce()
        {
            // Arrange
            var commerce = BuildCommerce("Commerce Three", "101000003");
            _dbContext.Commerces.Add(commerce);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(commerce).State = EntityState.Detached;

            var tracked = await _dbContext.Commerces.FindAsync(commerce.Id);
            tracked!.IsActive = false;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == commerce.Id);
            updated.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateAsync_Should_Not_Modify_Other_Commerces()
        {
            // Arrange
            var target = BuildCommerce("Commerce Four", "101000004");
            var untouched = BuildCommerce("Commerce Five", "101000005");
            _dbContext.Commerces.AddRange(target, untouched);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(target).State = EntityState.Detached;
            _dbContext.Entry(untouched).State = EntityState.Detached;

            var tracked = await _dbContext.Commerces.FindAsync(target.Id);
            tracked!.IsActive = false;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var stillActive = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == untouched.Id);
            stillActive.IsActive.Should().BeTrue();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
