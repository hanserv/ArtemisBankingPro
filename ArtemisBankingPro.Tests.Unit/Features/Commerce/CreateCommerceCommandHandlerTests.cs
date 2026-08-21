using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Commerce.Commands.Create;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Tests.Unit.Features.Commerce
{
    public class CreateCommerceCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        public CreateCommerceCommandHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CommerceCreatedResponseDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddValidatorsFromAssembly(typeof(CreateCommerceCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateCommerceCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task SeedCommerceAsync(string rnc, string email)
        {
            _dbContext.Commerces.Add(new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = "Existing Commerce",
                Email = email,
                PhoneNumber = "8095551234",
                Rnc = rnc,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = "admin-1"
            });
            await _dbContext.SaveChangesAsync();
        }

        private static CreateCommerceCommand BuildValidCommand() => new()
        {
            Name = "Tienda Demo",
            Description = "Test commerce",
            Email = $"{Guid.NewGuid()}@test.com",
            PhoneNumber = "8095551234",
            Rnc = Random.Shared.Next(100000000, 999999999).ToString(),
            AdminId = "admin-1"
        };

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Name_Is_Empty()
        {
            var command = BuildValidCommand();
            command.Name = "";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The commerce name is required.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Email_Has_Invalid_Format()
        {
            var command = BuildValidCommand();
            command.Email = "not-an-email";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The email must have a valid format.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PhoneNumber_Is_Empty()
        {
            var command = BuildValidCommand();
            command.PhoneNumber = "";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The phone number is required.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Rnc_Is_Empty()
        {
            var command = BuildValidCommand();
            command.Rnc = "";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The RNC is required.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_Rnc_Already_Exists()
        {
            // Arrange
            var command = BuildValidCommand();
            await SeedCommerceAsync(command.Rnc, "other@test.com");

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
            exception.Which.Message.Should().Be("A commerce with the same RNC already exists.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_Email_Already_Exists()
        {
            // Arrange
            var command = BuildValidCommand();
            await SeedCommerceAsync("999999999", command.Email);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
            exception.Which.Message.Should().Be("A commerce with the same email already exists.");
        }

        [Fact]
        public async Task Send_Should_Prioritize_Rnc_Conflict_When_Both_Rnc_And_Email_Already_Exist()
        {
            // Arrange
            var command = BuildValidCommand();
            await SeedCommerceAsync(command.Rnc, command.Email);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.Message.Should().Be("A commerce with the same RNC already exists.");
        }

        [Fact]
        public async Task Send_Should_Create_Commerce_As_Active_With_AdminId_From_Command()
        {
            // Arrange
            var command = BuildValidCommand();

            // Act
            var result = await _mediator.Send(command);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(command.Name);
            result.IsActive.Should().BeTrue();

            var created = await _dbContext.Commerces.FirstAsync(c => c.Rnc == command.Rnc);
            created.CreatedByAdminId.Should().Be("admin-1");
            created.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Send_Should_Not_Affect_Existing_Commerces()
        {
            // Arrange
            await SeedCommerceAsync("111111111", "untouched@test.com");
            var command = BuildValidCommand();

            // Act
            await _mediator.Send(command);

            // Assert
            var untouched = await _dbContext.Commerces.AsNoTracking().FirstAsync(c => c.Rnc == "111111111");
            untouched.Email.Should().Be("untouched@test.com");
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
