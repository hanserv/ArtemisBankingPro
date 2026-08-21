using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Cancel;
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

namespace ArtemisBankingPro.Tests.Unit.Features.CreditCard
{
    public class CancelCreditCardCommandTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        public CancelCreditCardCommandTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CreditCardDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICreditCardRepository, CreditCardRepository>();
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CancelCreditCardCommand).Assembly));
            services.AddValidatorsFromAssembly(typeof(CancelCreditCardCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.CreditCard> SeedCardAsync(
            CreditCardStatus status = CreditCardStatus.Active, decimal currentDebt = 0m)
        {
            var card = new Core.Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString(),
                ClientId = "client-1",
                CreditLimit = 50000m,
                CurrentDebt = currentDebt,
                ExpirationDate = "03/29",
                CvcHash = "hashed-cvc",
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;

            return card;
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Card_Does_Not_Exist()
        {
            var command = new CancelCreditCardCommand { CreditCardId = 999, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Is_Already_Cancelled()
        {
            var card = await SeedCardAsync(status: CreditCardStatus.Cancelled);
            var command = new CancelCreditCardCommand { CreditCardId = card.Id, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Has_Outstanding_Debt()
        {
            var card = await SeedCardAsync(currentDebt: 500m);
            var command = new CancelCreditCardCommand { CreditCardId = card.Id, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Cancel_Card_When_Active_And_Debt_Free()
        {
            var card = await SeedCardAsync(currentDebt: 0m);
            var command = new CancelCreditCardCommand { CreditCardId = card.Id, AdminId = "admin-1" };

            await _mediator.Send(command);

            var updatedCard = await _dbContext.CreditCards.FirstAsync(c => c.Id == card.Id);
            updatedCard.Status.Should().Be(CreditCardStatus.Cancelled);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
