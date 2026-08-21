using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Queries.GetById;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ArtemisBankingPro.Tests.Unit.Features.CreditCard
{
    public class GetCreditCardByIdQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetCreditCardByIdQueryHandlerTests()
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

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCreditCardByIdQuery).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.CreditCard> SeedCardAsync(string clientId = "client-1", decimal creditLimit = 50000m, decimal currentDebt = 10000m)
        {
            var card = new Core.Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString(),
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = "03/29",
                CvcHash = "hashed-cvc",
                CreatedByAdminId = "admin-1",
                Status = CreditCardStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;

            return card;
        }

        private async Task SeedConsumptionAsync(int creditCardId, decimal amount, ConsumptionStatus status, DateTime? consumptionDate = null)
        {
            _dbContext.CardConsumptions.Add(new CardConsumption
            {
                Id = 0,
                CreditCardId = creditCardId,
                CommerceId = null,
                Amount = amount,
                Status = status,
                ConsumptionDate = consumptionDate ?? DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Card_Does_Not_Exist()
        {
            var query = new GetCreditCardByIdQuery { Id = 999 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Return_Card_Details_With_Client_And_Admin_Names_Resolved()
        {
            // Arrange
            var card = await SeedCardAsync();

            // Act
            var result = await _mediator.Send(new GetCreditCardByIdQuery { Id = card.Id });

            // Assert
            result.Id.Should().Be(card.Id);
            result.ClientFullName.Should().Be("Client Name");
            result.CreatedByAdminName.Should().Be("Client Name");
            result.LastFourDigits.Should().Be(card.CardNumber[^4..]);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Consumptions_When_Card_Has_None()
        {
            // Arrange
            var card = await SeedCardAsync();

            // Act
            var result = await _mediator.Send(new GetCreditCardByIdQuery { Id = card.Id });

            // Assert
            result.Consumptions.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Return_Consumptions_Ordered_By_Most_Recent_First()
        {
            // Arrange
            var card = await SeedCardAsync();

            await SeedConsumptionAsync(card.Id, 100m, ConsumptionStatus.Approved, DateTime.UtcNow.AddDays(-2));
            await SeedConsumptionAsync(card.Id, 200m, ConsumptionStatus.Approved, DateTime.UtcNow.AddDays(-1));
            await SeedConsumptionAsync(card.Id, 300m, ConsumptionStatus.Rejected, DateTime.UtcNow);

            // Act
            var result = await _mediator.Send(new GetCreditCardByIdQuery { Id = card.Id });

            // Assert
            result.Consumptions.Should().HaveCount(3);
            result.Consumptions.Select(c => c.Amount).Should().ContainInOrder(300m, 200m, 100m);
        }

        [Fact]
        public async Task Send_Should_Include_Both_Approved_And_Rejected_Consumptions()
        {
            // Arrange
            var card = await SeedCardAsync();

            await SeedConsumptionAsync(card.Id, 100m, ConsumptionStatus.Approved);
            await SeedConsumptionAsync(card.Id, 200m, ConsumptionStatus.Rejected);

            // Act
            var result = await _mediator.Send(new GetCreditCardByIdQuery { Id = card.Id });

            // Assert
            result.Consumptions.Should().Contain(c => c.Status == ConsumptionStatus.Approved);
            result.Consumptions.Should().Contain(c => c.Status == ConsumptionStatus.Rejected);
        }

        [Fact]
        public async Task Send_Should_Not_Include_Consumptions_From_Other_Cards()
        {
            // Arrange
            var card1 = await SeedCardAsync("client-1");
            var card2 = await SeedCardAsync("client-2");

            await SeedConsumptionAsync(card1.Id, 100m, ConsumptionStatus.Approved);
            await SeedConsumptionAsync(card2.Id, 200m, ConsumptionStatus.Approved);

            // Act
            var result = await _mediator.Send(new GetCreditCardByIdQuery { Id = card1.Id });

            // Assert
            result.Consumptions.Should().ContainSingle();
            result.Consumptions.Single().Amount.Should().Be(100m);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
