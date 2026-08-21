using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Update;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Tests.Unit.Features.Commerce
{
    public class UpdateCommerceCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        public UpdateCommerceCommandHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();

            services.AddValidatorsFromAssembly(typeof(UpdateCommerceCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UpdateCommerceCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Commerce> SeedCommerceAsync(string rnc, string email, string name = "Commerce")
        {
            var commerce = new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = name,
                Email = email,
                PhoneNumber = "8095551234",
                Rnc = rnc,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = "admin-1"
            };
            _dbContext.Commerces.Add(commerce);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(commerce).State = EntityState.Detached;
            return commerce;
        }

        private static UpdateCommerceCommand BuildValidCommand(int id, string rnc, string email) => new()
        {
            Id = id,
            Name = "Tienda Demo Updated",
            Description = "Updated",
            Email = email,
            PhoneNumber = "8095555678",
            Rnc = rnc
        };

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Name_Is_Empty()
        {
            var command = BuildValidCommand(1, "101999999", "test@test.com");
            command.Name = "";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The commerce name is required.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Email_Has_Invalid_Format()
        {
            var command = BuildValidCommand(1, "101999999", "not-an-email");

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The email must have a valid format.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Commerce_Does_Not_Exist()
        {
            var command = BuildValidCommand(999, "101999999", "test@test.com");

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_Rnc_Belongs_To_Another_Commerce()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000001", "target@test.com");
            var other = await SeedCommerceAsync("100000002", "other@test.com");

            var command = BuildValidCommand(target.Id, other.Rnc, "target-new@test.com");

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
            exception.Which.Message.Should().Be("The RNC belongs to another commerce.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_Email_Belongs_To_Another_Commerce()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000003", "target2@test.com");
            var other = await SeedCommerceAsync("100000004", "other2@test.com");

            var command = BuildValidCommand(target.Id, "100000005", other.Email);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
            exception.Which.Message.Should().Be("The email belongs to another commerce.");
        }

        [Fact]
        public async Task Send_Should_Allow_Updating_Commerce_Keeping_Its_Own_Rnc_And_Email()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000006", "target3@test.com");
            var command = BuildValidCommand(target.Id, target.Rnc, target.Email);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            await act.Should().NotThrowAsync();
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == target.Id);
            updated.Name.Should().Be("Tienda Demo Updated");
        }

        [Fact]
        public async Task Send_Should_Update_All_Editable_Fields()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000007", "target4@test.com");
            var command = BuildValidCommand(target.Id, "100000008", "updated4@test.com");

            // Act
            await _mediator.Send(command);

            // Assert
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == target.Id);
            updated.Name.Should().Be(command.Name);
            updated.Description.Should().Be(command.Description);
            updated.Email.Should().Be(command.Email);
            updated.PhoneNumber.Should().Be(command.PhoneNumber);
            updated.Rnc.Should().Be(command.Rnc);
        }

        [Fact]
        public async Task Send_Should_Not_Modify_Status()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000009", "target5@test.com");
            var command = BuildValidCommand(target.Id, "100000010", "updated5@test.com");

            // Act
            await _mediator.Send(command);

            // Assert
            var updated = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == target.Id);
            updated.IsActive.Should().BeTrue(); // sin cambios, sigue como estaba al sembrarlo
        }

        [Fact]
        public async Task Send_Should_Not_Affect_Other_Commerces()
        {
            // Arrange
            var target = await SeedCommerceAsync("100000011", "target6@test.com");
            var untouched = await SeedCommerceAsync("100000012", "untouched6@test.com", "Untouched");

            var command = BuildValidCommand(target.Id, "100000013", "updated6@test.com");

            // Act
            await _mediator.Send(command);

            // Assert
            var stillUntouched = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Id == untouched.Id);
            stillUntouched.Name.Should().Be("Untouched");
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
