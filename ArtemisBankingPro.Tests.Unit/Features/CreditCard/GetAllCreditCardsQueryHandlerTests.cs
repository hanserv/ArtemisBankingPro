using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetAll;
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

namespace ArtemisBankingPro.Tests.Unit.Features.CreditCard
{
    public class GetAllCreditCardsQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetAllCreditCardsQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CreditCardDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICreditCardRepository, CreditCardRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAllCreditCardsQuery).Assembly));
            services.AddValidatorsFromAssembly(typeof(GetAllCreditCardsQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.CreditCard> SeedCardAsync(
            string clientId = "client-1", CreditCardStatus status = CreditCardStatus.Active,
            DateTime? createdAt = null, decimal creditLimit = 50000m, decimal currentDebt = 0m)
        {
            var card = new Core.Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = Random.Shared.Next(0, 999999999).ToString("D16"),
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = "03/29",
                CvcHash = "hashed-cvc",
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;

            return card;
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Page_Is_Invalid()
        {
            var query = new GetAllCreditCardsQuery { Page = 0, PageSize = 10 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PageSize_Is_Invalid()
        {
            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 0 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Default_To_Active_Cards_Only_When_No_Filters_Are_Sent()
        {
            // Arrange
            await SeedCardAsync(status: CreditCardStatus.Active);
            await SeedCardAsync(status: CreditCardStatus.Cancelled);

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(1);
            result.Items.Single().Status.Should().Be(CreditCardStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Result_When_Identification_Does_Not_Match_Any_Client()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync((string?)null);

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Filter_By_Identification_And_Include_All_Statuses_When_Status_Is_Null()
        {
            // Arrange
            await SeedCardAsync("client-1", status: CreditCardStatus.Active, createdAt: DateTime.UtcNow.AddDays(-2));
            await SeedCardAsync("client-1", status: CreditCardStatus.Cancelled, createdAt: DateTime.UtcNow.AddDays(-1));
            await SeedCardAsync("client-2", status: CreditCardStatus.Active);

            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-1111111-1")).ReturnsAsync("client-1");

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10, Status = null, Identification = "001-1111111-1" };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(2);
            result.Items.All(i => i.ClientId == "client-1").Should().BeTrue();
            result.Items.First().Status.Should().Be(CreditCardStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Include_All_Statuses_When_Status_Filter_Is_All_Even_Without_Identification()
        {
            // Arrange
            await SeedCardAsync(status: CreditCardStatus.Active);
            await SeedCardAsync(status: CreditCardStatus.Cancelled);

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10, Status = CreditCardStatusFilter.All };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(2);
        }

        [Fact]
        public async Task Send_Should_Filter_By_Cancelled_Status_When_Explicitly_Requested()
        {
            // Arrange
            await SeedCardAsync(status: CreditCardStatus.Active);
            await SeedCardAsync(status: CreditCardStatus.Cancelled);

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10, Status = CreditCardStatusFilter.Cancelled };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(1);
            result.Items.Single().Status.Should().Be(CreditCardStatus.Cancelled);
        }

        [Fact]
        public async Task Send_Should_Map_LastFourDigits_And_MaskedCardNumber_Without_Exposing_Full_Card_Number()
        {
            // Arrange
            var card = await SeedCardAsync();

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            var dto = result.Items.Single();
            dto.LastFourDigits.Should().Be(card.CardNumber[^4..]);
            dto.MaskedCardNumber.Should().EndWith(dto.LastFourDigits);
            dto.MaskedCardNumber.Should().NotContain(card.CardNumber);
        }

        [Fact]
        public async Task Send_Should_Respect_PageSize_Cap_Of_20()
        {
            // Arrange
            for (var i = 0; i < 25; i++)
            {
                await SeedCardAsync(clientId: $"client-{i}");
            }

            var query = new GetAllCreditCardsQuery { Page = 1, PageSize = 50 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.PageSize.Should().Be(20);
            result.Items.Should().HaveCount(20);
            result.TotalRecords.Should().Be(25);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
