using System.Net;
using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.HermesPay.Commands.ProcessPayment;
using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
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

namespace ArtemisBankingPro.Tests.Unit.Features.HermesPay
{
    public class ProcessCommercePaymentCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        private const string ValidCardNumber = "1589963258467598";
        private const string ValidMonth = "02";
        private const string ValidYear = "2028";
        private const string ValidCvc = "859";

        public ProcessCommercePaymentCommandHandlerTests()
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
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>())).ReturnsAsync(Result.Success());

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICreditCardRepository, CreditCardRepository>();
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddScoped<ICardConsumptionRepository, CardConsumptionRepository>();
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_emailServiceMock.Object);

            services.AddValidatorsFromAssembly(typeof(ProcessCommercePaymentCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessCommercePaymentCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.CreditCard> SeedCardAsync(
            string clientId = "client-1", decimal creditLimit = 5000m, decimal currentDebt = 0m,
            CreditCardStatus status = CreditCardStatus.Active, string expirationDate = "02/28", string cvc = ValidCvc)
        {
            var card = new Core.Domain.Entities.CreditCard
            {
                Id = 0,
                CardNumber = ValidCardNumber,
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = expirationDate,
                CvcHash = Sha256Helper.Hash(cvc),
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;
            return card;
        }

        private async Task<Core.Domain.Entities.Commerce> SeedCommerceAsync(bool isActive = true)
        {
            var commerce = new Core.Domain.Entities.Commerce
            {
                Id = 0,
                Name = "Tienda Demo",
                Email = $"{Guid.NewGuid()}@test.com",
                PhoneNumber = "8095551234",
                Rnc = Random.Shared.Next(100000000, 999999999).ToString(),
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = "admin-1"
            };
            _dbContext.Commerces.Add(commerce);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(commerce).State = EntityState.Detached;
            return commerce;
        }

        private async Task<Core.Domain.Entities.SavingsAccount> SeedPrincipalAccountAsync(string clientId, decimal balance = 0m, SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = $"1{Random.Shared.Next(10000000, 99999999)}",
                ClientId = clientId,
                Balance = balance,
                Type = SavingsAccountType.Principal,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

        private async Task<(Core.Domain.Entities.CreditCard Card, Core.Domain.Entities.Commerce Commerce, Core.Domain.Entities.SavingsAccount Account)> SeedFullyValidScenarioAsync(
            decimal creditLimit = 5000m, decimal currentDebt = 0m, decimal accountBalance = 0m)
        {
            var card = await SeedCardAsync(creditLimit: creditLimit, currentDebt: currentDebt);
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("commerce-user-1");
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(card.ClientId))
                .ReturnsAsync(new UserBasicInfoDto { Id = card.ClientId, FullName = "Card Holder", Identification = "001", Email = "holder@test.com" });
            var account = await SeedPrincipalAccountAsync("commerce-user-1", accountBalance);
            return (card, commerce, account);
        }

        private static ProcessCommercePaymentCommand BuildValidCommand(int commerceId, decimal amount = 500m) => new()
        {
            CommerceId = commerceId,
            PerformedByUserId = "commerce-user-1",
            CardNumber = ValidCardNumber,
            MonthExpirationCard = ValidMonth,
            YearExpirationCard = ValidYear,
            Cvc = ValidCvc,
            TransactionAmount = amount
        };

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_CardNumber_Is_Not_16_Digits()
        {
            var command = BuildValidCommand(1);
            command.CardNumber = "12345";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The card number must contain exactly 16 digits.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_MonthExpirationCard_Is_Invalid()
        {
            var command = BuildValidCommand(1);
            command.MonthExpirationCard = "13";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The expiration month must be a valid value between 01 and 12.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Cvc_Is_Not_3_Digits()
        {
            var command = BuildValidCommand(1);
            command.Cvc = "12";

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The CVC must contain exactly 3 digits.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_TransactionAmount_Is_Zero()
        {
            var command = BuildValidCommand(1, amount: 0m);

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The transaction amount must be greater than zero.");
        }


        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Does_Not_Exist()
        {
            var command = BuildValidCommand(1);

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The card does not exist.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Is_Not_Active()
        {
            // Arrange
            await SeedCardAsync(status: CreditCardStatus.Cancelled);
            var command = BuildValidCommand(1);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The card is not active.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Expiration_Does_Not_Match()
        {
            // Arrange
            await SeedCardAsync(expirationDate: "05/30");
            var command = BuildValidCommand(1);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The card expiration data does not match.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Card_Is_Expired()
        {
            // Arrange
            await SeedCardAsync(expirationDate: "01/20"); // Jan 2020, vencida
            var command = BuildValidCommand(1);
            command.MonthExpirationCard = "01";
            command.YearExpirationCard = "2020";

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The card is expired.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Cvc_Does_Not_Match()
        {
            // Arrange
            await SeedCardAsync(cvc: "111");
            var command = BuildValidCommand(1); 

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The CVC does not match.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Commerce_Is_Inactive()
        {
            // Arrange
            await SeedCardAsync();
            var commerce = await SeedCommerceAsync(isActive: false);
            var command = BuildValidCommand(commerce.Id);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The commerce is not active.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Commerce_Has_No_Associated_User()
        {
            // Arrange
            await SeedCardAsync();
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync((string?)null);
            var command = BuildValidCommand(commerce.Id);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The commerce does not have an associated user.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Associated_User_Has_No_Active_Principal_Account()
        {
            // Arrange
            await SeedCardAsync();
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("commerce-user-x");
            // sin cuenta principal sembrada
            var command = BuildValidCommand(commerce.Id);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The commerce's associated user does not have an active principal savings account.");
        }


        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Commerce_Does_Not_Exist_But_Card_Is_Valid()
        {
            await SeedCardAsync();
            var command = BuildValidCommand(999);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Process_Payment_Successfully()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync(creditLimit: 5000m, currentDebt: 100m, accountBalance: 1000m);
            var command = BuildValidCommand(commerce.Id, amount: 500m);

            // Act
            await _mediator.Send(command);

            // Assert
            var updatedCard = await _dbContext.CreditCards.AsNoTracking().FirstAsync(c => c.Id == card.Id);
            updatedCard.CurrentDebt.Should().Be(600m);

            var updatedAccount = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == account.Id);
            updatedAccount.Balance.Should().Be(1500m);
        }

        [Fact]
        public async Task Send_Should_Create_Approved_Consumption_And_HermesPayment_Transaction_On_Success()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync();
            var command = BuildValidCommand(commerce.Id, amount: 250m);

            // Act
            await _mediator.Send(command);

            // Assert
            var consumption = await _dbContext.CardConsumptions.AsNoTracking().FirstAsync(c => c.CreditCardId == card.Id);
            consumption.Status.Should().Be(ConsumptionStatus.Approved);
            consumption.Amount.Should().Be(250m);
            consumption.CommerceId.Should().Be(commerce.Id);

            var transaction = await _dbContext.Transactions.AsNoTracking().FirstAsync(t => t.SavingsAccountId == account.Id);
            transaction.Category.Should().Be(TransactionCategory.HermesPayment);
            transaction.Type.Should().Be(TransactionType.Credit);
            transaction.Origin.Should().Be(card.CardNumber[^4..]);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_And_Create_Rejected_Consumption_When_Amount_Exceeds_Available_Credit()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync(creditLimit: 1000m, currentDebt: 900m); // disponible: 100
            var command = BuildValidCommand(commerce.Id, amount: 500m);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

            var consumption = await _dbContext.CardConsumptions.AsNoTracking().FirstAsync(c => c.CreditCardId == card.Id);
            consumption.Status.Should().Be(ConsumptionStatus.Rejected);
            consumption.Amount.Should().Be(500m);
        }

        [Fact]
        public async Task Send_Should_Not_Modify_Card_Or_Account_When_Amount_Exceeds_Available_Credit()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync(creditLimit: 1000m, currentDebt: 900m, accountBalance: 500m);
            var command = BuildValidCommand(commerce.Id, amount: 500m);

            // Act
            var act = async () => await _mediator.Send(command);
            await act.Should().ThrowAsync<ApiException>();

            // Assert
            var untouchedCard = await _dbContext.CreditCards.AsNoTracking().FirstAsync(c => c.Id == card.Id);
            untouchedCard.CurrentDebt.Should().Be(900m);

            var untouchedAccount = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == account.Id);
            untouchedAccount.Balance.Should().Be(500m);
        }

        [Fact]
        public async Task Send_Should_Send_Notification_Emails_To_CardHolder_And_Commerce_On_Success()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync();
            var command = BuildValidCommand(commerce.Id, amount: 300m);

            // Act
            await _mediator.Send(command);

            // Assert
            _emailServiceMock.Verify(s => s.SendAsync(It.Is<EmailRequestDto>(e => e.To == "holder@test.com")), Times.Once);
            _emailServiceMock.Verify(s => s.SendAsync(It.Is<EmailRequestDto>(e => e.To == commerce.Email)), Times.Once);
        }

        [Fact]
        public async Task Send_Should_Not_Throw_When_Email_Sending_Fails()
        {
            // Arrange
            var (card, commerce, account) = await SeedFullyValidScenarioAsync();
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>())).ThrowsAsync(new Exception("SMTP down"));
            var command = BuildValidCommand(commerce.Id, amount: 100m);

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            await act.Should().NotThrowAsync();
            var updatedCard = await _dbContext.CreditCards.AsNoTracking().FirstAsync(c => c.Id == card.Id);
            updatedCard.CurrentDebt.Should().Be(100m); 
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
