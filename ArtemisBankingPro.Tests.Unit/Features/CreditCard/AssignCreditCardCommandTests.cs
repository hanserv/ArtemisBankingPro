using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.CreditCard.Commands.Assign;
using ArtemisBankingPro.Core.Application.Interfaces;
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
    public class AssignCreditCardCommandTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<ICardNumberGenerator> _cardNumberGeneratorMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        private const string ClientId = "client-1";

        public AssignCreditCardCommandTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _cardNumberGeneratorMock = new Mock<ICardNumberGenerator>();
            _emailServiceMock = new Mock<IEmailService>();

            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(ClientId))
                .ReturnsAsync(new UserBasicInfoDto { Id = ClientId, FullName = "Client Name", Email = "client@email.com", Identification = "123456789" });
            _cardNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("1234567890123456");

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CreditCardDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICreditCardRepository, CreditCardRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_cardNumberGeneratorMock.Object);
            services.AddSingleton(_emailServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssignCreditCardCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private static AssignCreditCardCommand ValidCommand() => new()
        {
            ClientId = ClientId,
            CreditLimit = 50000m,
            AdminId = "admin-1"
        };

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Client_Does_Not_Exist()
        {
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(ClientId)).ReturnsAsync((UserBasicInfoDto?)null);

            var act = async () => await _mediator.Send(ValidCommand());

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Create_Card_With_Zero_Initial_Debt_And_Active_Status()
        {
            var result = await _mediator.Send(ValidCommand());

            result.CurrentDebt.Should().Be(0m);
            result.Status.Should().Be(Core.Domain.Common.Enums.CreditCardStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Set_Expiration_Date_Three_Years_From_Now()
        {
            var result = await _mediator.Send(ValidCommand());

            var expected = DateTime.UtcNow.AddYears(3).ToString("MM/yy");
            result.ExpirationDate.Should().Be(expected);
        }

        [Fact]
        public async Task Send_Should_Persist_Card_Number_From_Generator_And_Never_Expose_The_Cvc()
        {
            var result = await _mediator.Send(ValidCommand());

            var card = await _dbContext.CreditCards.FirstAsync(c => c.ClientId == ClientId);
            card.CardNumber.Should().Be("1234567890123456");
            card.CvcHash.Should().NotBeNullOrEmpty();

            result.LastFourDigits.Should().Be("3456");
        }

        [Fact]
        public async Task Send_Should_Link_Card_To_The_Authenticated_Admin()
        {
            var command = ValidCommand();
            command.AdminId = "admin-42";

            await _mediator.Send(command);

            var card = await _dbContext.CreditCards.FirstAsync(c => c.ClientId == ClientId);
            card.CreatedByAdminId.Should().Be("admin-42");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_Card_Number_Generation_Fails()
        {
            _cardNumberGeneratorMock.Setup(g => g.GenerateAsync())
                .ThrowsAsync(new InvalidOperationException("Could not generate a unique 16-digit card number after several attempts."));

            var act = async () => await _mediator.Send(ValidCommand());

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Send_Should_Return_Card_Even_When_Notification_Email_Fails()
        {
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>()))
                .ThrowsAsync(new Exception("SMTP unavailable"));

            var result = await _mediator.Send(ValidCommand());

            result.Should().NotBeNull();
            var card = await _dbContext.CreditCards.FirstAsync(c => c.ClientId == ClientId);
            card.Should().NotBeNull();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
