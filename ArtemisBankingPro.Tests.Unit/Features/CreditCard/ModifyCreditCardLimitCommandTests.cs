using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.ModifyLimit;
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
    public class ModifyCreditCardLimitCommandTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        public ModifyCreditCardLimitCommandTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _emailServiceMock = new Mock<IEmailService>();

            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserBasicInfoDto { Id = "client-1", FullName = "Client Name", Email = "client@email.com", Identification = "123456789" });

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CreditCardDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICreditCardRepository, CreditCardRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_emailServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ModifyCreditCardLimitCommand).Assembly));
            services.AddValidatorsFromAssembly(typeof(ModifyCreditCardLimitCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.CreditCard> SeedCardAsync(
            CreditCardStatus status = CreditCardStatus.Active, decimal creditLimit = 50000m, decimal currentDebt = 10000m)
        {
            var card = new Core.Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString(),
                ClientId = "client-1",
                CreditLimit = creditLimit,
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
        public async Task Send_Should_Throw_ValidationException_When_CreditLimit_Is_Not_Greater_Than_Zero()
        {
            var card = await SeedCardAsync();
            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 0, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Is_Cancelled()
        {
            var card = await SeedCardAsync(status: CreditCardStatus.Cancelled);
            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 75000m, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_New_Limit_Is_Lower_Than_Current_Debt()
        {
            var card = await SeedCardAsync(currentDebt: 20000m);
            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 15000m, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Card_Does_Not_Exist_In_Handler()
        {
            var command = new ModifyCreditCardLimitCommand { CreditCardId = 999, CreditLimit = 75000m, AdminId = "admin-1" };

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Update_Limit_When_New_Limit_Equals_Current_Debt()
        {
            var card = await SeedCardAsync(currentDebt: 20000m);
            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 20000m, AdminId = "admin-1" };

            await _mediator.Send(command);

            var updatedCard = await _dbContext.CreditCards.FirstAsync(c => c.Id == card.Id);
            updatedCard.CreditLimit.Should().Be(20000m);
        }

        [Fact]
        public async Task Send_Should_Update_Limit_Successfully_When_All_Rules_Pass()
        {
            var card = await SeedCardAsync(creditLimit: 50000m, currentDebt: 10000m);
            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 75000m, AdminId = "admin-1" };

            await _mediator.Send(command);

            var updatedCard = await _dbContext.CreditCards.FirstAsync(c => c.Id == card.Id);
            updatedCard.CreditLimit.Should().Be(75000m);
        }

        [Fact]
        public async Task Send_Should_Update_Limit_Even_When_Notification_Email_Fails()
        {
            var card = await SeedCardAsync();
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>()))
                .ThrowsAsync(new Exception("SMTP unavailable"));

            var command = new ModifyCreditCardLimitCommand { CreditCardId = card.Id, CreditLimit = 75000m, AdminId = "admin-1" };
            var act = async () => await _mediator.Send(command);

            await act.Should().NotThrowAsync();

            var updatedCard = await _dbContext.CreditCards.FirstAsync(c => c.Id == card.Id);
            updatedCard.CreditLimit.Should().Be(75000m);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
