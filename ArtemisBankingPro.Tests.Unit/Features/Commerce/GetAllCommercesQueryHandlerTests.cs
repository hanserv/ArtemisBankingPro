using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Commerce;
using ArtemisBankingPro.Core.Application.Features.Commerce.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
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
    public class GetAllCommercesQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetAllCommercesQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _basicUserInfoServiceMock
                .Setup(s => s.GetCommerceIdsWithAssociatedUserAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new HashSet<int>());

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CommerceDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddValidatorsFromAssembly(typeof(GetAllCommercesQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAllCommercesQuery).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task SeedCommerceAsync(string name, bool isActive, DateTime? createdAt = null)
        {
            _dbContext.Commerces.Add(new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = name,
                Email = $"{Guid.NewGuid()}@test.com",
                PhoneNumber = "8095551234",
                Rnc = Random.Shared.Next(100000000, 999999999).ToString(),
                IsActive = isActive,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                CreatedByAdminId = "admin-1"
            });
            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Page_Is_Zero()
        {
            var query = new GetAllCommercesQuery { Page = 0 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The page parameter must be greater than zero.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PageSize_Is_Zero()
        {
            var query = new GetAllCommercesQuery { PageSize = 0 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The pageSize parameter must be greater than zero.");
        }

        [Fact]
        public async Task Send_Should_Return_Only_Active_Commerces_By_Default()
        {
            // Arrange
            await SeedCommerceAsync("Active Commerce", true);
            await SeedCommerceAsync("Inactive Commerce", false);

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery());

            // Assert
            result.Items.Should().ContainSingle();
            result.Items[0].Name.Should().Be("Active Commerce");
        }

        [Fact]
        public async Task Send_Should_Return_Only_Inactive_Commerces_When_Filter_Is_Inactive()
        {
            // Arrange
            await SeedCommerceAsync("Active Commerce", true);
            await SeedCommerceAsync("Inactive Commerce", false);

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery { Status = CommerceStatusFilter.Inactive });

            // Assert
            result.Items.Should().ContainSingle();
            result.Items[0].Name.Should().Be("Inactive Commerce");
        }

        [Fact]
        public async Task Send_Should_Return_All_Commerces_When_Filter_Is_All()
        {
            // Arrange
            await SeedCommerceAsync("Active Commerce", true);
            await SeedCommerceAsync("Inactive Commerce", false);

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery { Status = CommerceStatusFilter.All });

            // Assert
            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task Send_Should_Return_Commerces_Ordered_By_Most_Recent_First()
        {
            // Arrange
            await SeedCommerceAsync("Older", true, DateTime.UtcNow.AddDays(-2));
            await SeedCommerceAsync("Newer", true, DateTime.UtcNow);

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery { Status = CommerceStatusFilter.All });

            // Assert
            result.Items.Select(c => c.Name).Should().ContainInOrder("Newer", "Older");
        }

        [Fact]
        public async Task Send_Should_Cap_PageSize_At_Twenty_When_Requested_Higher()
        {
            // Arrange
            for (var i = 0; i < 25; i++)
            {
                await SeedCommerceAsync($"Commerce {i}", true);
            }

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery { PageSize = 50 });

            // Assert
            result.PageSize.Should().Be(20);
            result.Items.Should().HaveCount(20);
            result.TotalRecords.Should().Be(25);
        }

        [Fact]
        public async Task Send_Should_Return_Second_Page_With_Remaining_Commerces()
        {
            // Arrange
            for (var i = 0; i < 25; i++)
            {
                await SeedCommerceAsync($"Commerce {i}", true, DateTime.UtcNow.AddMinutes(-i));
            }

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery { Page = 2, PageSize = 20 });

            // Assert
            result.Items.Should().HaveCount(5);
        }

        [Fact]
        public async Task Send_Should_Set_HasAssociatedUser_True_When_Commerce_Has_Associated_User()
        {
            // Arrange
            await SeedCommerceAsync("With User", true);
            var commerce = await _dbContext.Commerces.FirstAsync(c => c.Name == "With User");

            _basicUserInfoServiceMock
                .Setup(s => s.GetCommerceIdsWithAssociatedUserAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new HashSet<int> { commerce.Id });

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery());

            // Assert
            result.Items.Single().HasAssociatedUser.Should().BeTrue();
        }

        [Fact]
        public async Task Send_Should_Set_HasAssociatedUser_False_When_Commerce_Has_No_Associated_User()
        {
            // Arrange
            await SeedCommerceAsync("Without User", true);

            // Act
            var result = await _mediator.Send(new GetAllCommercesQuery());

            // Assert
            result.Items.Single().HasAssociatedUser.Should().BeFalse();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
