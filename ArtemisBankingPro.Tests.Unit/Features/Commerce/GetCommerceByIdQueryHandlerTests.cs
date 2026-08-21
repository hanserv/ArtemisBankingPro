using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetById;
using ArtemisBankingPro.Core.Application.Interfaces;
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
using Moq;

namespace ArtemisBankingPro.Tests.Unit.Features.Commerce
{
    public class GetCommerceByIdQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetCommerceByIdQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CommerceDetailsDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddValidatorsFromAssembly(typeof(GetCommerceByIdQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCommerceByIdQuery).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Commerce> SeedCommerceAsync(string? associatedUserId = null)
        {
            var commerce = new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = "Tienda Demo",
                Description = "Test commerce",
                Email = $"{Guid.NewGuid()}@test.com",
                PhoneNumber = "8095551234",
                Rnc = Random.Shared.Next(100000000, 999999999).ToString(),
                IsActive = true,
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
        public async Task Send_Should_Throw_ValidationException_When_Id_Is_Zero()
        {
            var query = new GetCommerceByIdQuery { Id = 0 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The id parameter must be greater than zero.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Commerce_Does_Not_Exist()
        {
            var query = new GetCommerceByIdQuery { Id = 999 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Return_Commerce_Details_Without_AssociatedUser_When_None_Exists()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(associatedUserId: null);

            // Act
            var result = await _mediator.Send(new GetCommerceByIdQuery { Id = commerce.Id });

            // Assert
            result.Id.Should().Be(commerce.Id);
            result.AssociatedUser.Should().BeNull();
            _basicUserInfoServiceMock.Verify(s => s.GetCommerceAssociatedUserInfoAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Send_Should_Return_Commerce_Details_With_AssociatedUser_When_Exists()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(associatedUserId: "user-1");

            _basicUserInfoServiceMock
                .Setup(s => s.GetCommerceAssociatedUserInfoAsync("user-1"))
                .ReturnsAsync(new CommerceAssociatedUserDto
                {
                    Id = "user-1",
                    UserName = "commerce01",
                    Email = "commerce01@artemis.com",
                    IsActive = true
                });

            // Act
            var result = await _mediator.Send(new GetCommerceByIdQuery { Id = commerce.Id });

            // Assert
            result.AssociatedUser.Should().NotBeNull();
            result.AssociatedUser!.Id.Should().Be("user-1");
            result.AssociatedUser.UserName.Should().Be("commerce01");
        }

        [Fact]
        public async Task Send_Should_Not_Call_AssociatedUserService_When_AssociatedUserId_Is_Empty_String()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(associatedUserId: "");

            // Act
            var result = await _mediator.Send(new GetCommerceByIdQuery { Id = commerce.Id });

            // Assert
            result.AssociatedUser.Should().BeNull();
            _basicUserInfoServiceMock.Verify(s => s.GetCommerceAssociatedUserInfoAsync(It.IsAny<string>()), Times.Never);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
