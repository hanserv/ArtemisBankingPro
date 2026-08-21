using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.ChangeStatus;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ArtemisBankingPro.Tests.Unit.Features.Commerce
{
    public class ChangeCommerceStatusCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public ChangeCommerceStatusCommandHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);

            services.AddValidatorsFromAssembly(typeof(ChangeCommerceStatusCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ChangeCommerceStatusCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Commerce> SeedCommerceAsync(bool isActive, string? associatedUserId = null)
        {
            var commerce = new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = "Commerce",
                Email = $"{Guid.NewGuid()}@test.com",
                PhoneNumber = "8095551234",
                Rnc = Random.Shared.Next(100000000, 999999999).ToString(),
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = "admin-1",
                AssociatedUserId = associatedUserId
            };
            _dbContext.Commerces.Add(commerce);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(commerce).State = EntityState.Detached;
            return commerce;
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Commerce_Does_Not_Exist()
        {
            var command = new ChangeCommerceStatusCommand { Id = 999, Status = false };

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Activate_Commerce_When_Status_Is_True()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: false);
            var command = new ChangeCommerceStatusCommand { Id = commerce.Id, Status = true };

            // Act
            await _mediator.Send(command);

            // Assert
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == commerce.Id);
            updated.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Send_Should_Deactivate_Commerce_When_Status_Is_False()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: true);
            var command = new ChangeCommerceStatusCommand { Id = commerce.Id, Status = false };

            // Act
            await _mediator.Send(command);

            // Assert
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == commerce.Id);
            updated.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task Send_Should_Deactivate_Associated_User_When_Deactivating_Commerce_With_User()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: true, associatedUserId: "user-1");
            var command = new ChangeCommerceStatusCommand { Id = commerce.Id, Status = false };

            // Act
            await _mediator.Send(command);

            // Assert
            _basicUserInfoServiceMock.Verify(s => s.DeactivateUserAsync("user-1"), Times.Once);
        }

        [Fact]
        public async Task Send_Should_Not_Deactivate_User_When_Reactivating_Commerce()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: false, associatedUserId: "user-2");
            var command = new ChangeCommerceStatusCommand { Id = commerce.Id, Status = true };

            // Act
            await _mediator.Send(command);

            // Assert
            _basicUserInfoServiceMock.Verify(s => s.DeactivateUserAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Send_Should_Not_Call_DeactivateUser_When_Commerce_Has_No_Associated_User()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: true, associatedUserId: null);
            var command = new ChangeCommerceStatusCommand { Id = commerce.Id, Status = false };

            // Act
            await _mediator.Send(command);

            // Assert
            _basicUserInfoServiceMock.Verify(s => s.DeactivateUserAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Send_Should_Not_Affect_Other_Commerces()
        {
            // Arrange
            var target = await SeedCommerceAsync(isActive: true);
            var untouched = await SeedCommerceAsync(isActive: true);
            var command = new ChangeCommerceStatusCommand { Id = target.Id, Status = false };

            // Act
            await _mediator.Send(command);

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
