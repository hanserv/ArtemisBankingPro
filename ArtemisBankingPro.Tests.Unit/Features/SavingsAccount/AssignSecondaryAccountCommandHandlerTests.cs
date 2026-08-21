using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.AssignSecondary;
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

namespace ArtemisBankingPro.Tests.Unit.Features.SavingsAccount
{
    public class AssignSecondaryAccountCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IAccountNumberGenerator> _accountNumberGeneratorMock;

        public AssignSecondaryAccountCommandHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _accountNumberGeneratorMock = new Mock<IAccountNumberGenerator>();
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("200000001");

            var config = new TypeAdapterConfig();
            config.Scan(typeof(SavingsAccountDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_accountNumberGeneratorMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddValidatorsFromAssembly(typeof(AssignSecondaryAccountCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssignSecondaryAccountCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.SavingsAccount> SeedPrincipalAccountAsync(string clientId, SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = $"1{Random.Shared.Next(10000000, 99999999)}",
                ClientId = clientId,
                Balance = 1000m,
                Type = SavingsAccountType.Principal,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;

            return account;
        }

        private void SetupActiveClient(string clientId, string fullName = "Client Name")
        {
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(clientId))
                .ReturnsAsync(new UserBasicInfoDto { Id = clientId, FullName = fullName, Identification = "001", Email = "client@test.com" });
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync(clientId)).ReturnsAsync(true);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Client_Does_Not_Exist()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("client-1")).ReturnsAsync((UserBasicInfoDto?)null);
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync((bool?)null);

            var command = new AssignSecondaryAccountCommand { ClientId = "client-1", InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_ClientId_Is_Empty()
        {
            // Arrange
            var command = new AssignSecondaryAccountCommand { ClientId = "", InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("You must select a client to continue.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_InitialBalance_Is_Negative()
        {
            // Arrange
            var command = new AssignSecondaryAccountCommand { ClientId = "client-2", InitialBalance = -100m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The initial balance cannot be negative.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Client_Is_Inactive()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-3")).ReturnsAsync(false);

            var command = new AssignSecondaryAccountCommand { ClientId = "client-3", InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("Savings accounts can only be assigned to active clients.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Client_Has_No_Active_Principal_Account()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-4")).ReturnsAsync(true);
            // sin cuenta principal creada

            var command = new AssignSecondaryAccountCommand { ClientId = "client-4", InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The client must have an active principal savings account before a secondary account can be assigned.");
        }

        [Fact]
        public async Task Send_Should_Create_Secondary_Account_With_Zero_Balance_And_No_Transaction()
        {
            // Arrange
            var clientId = "client-5";
            SetupActiveClient(clientId);
            await SeedPrincipalAccountAsync(clientId);

            var command = new AssignSecondaryAccountCommand { ClientId = clientId, InitialBalance = 0m, CreatedByAdminId = "admin-1" };

            // Act
            var result = await _mediator.Send(command);

            // Assert
            result.Should().NotBeNull();
            result.ClientFullName.Should().Be("Client Name");
            var createdAccount = await _dbContext.SavingsAccounts.FirstAsync(a => a.AccountNumber == "200000001");
            createdAccount.Type.Should().Be(SavingsAccountType.Secondary);
            var transactions = await _dbContext.Transactions.Where(t => t.SavingsAccountId == createdAccount.Id).ToListAsync();
            transactions.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Create_Secondary_Account_With_Initial_Credit_And_Opening_Transaction()
        {
            // Arrange
            var clientId = "client-6";
            SetupActiveClient(clientId);
            await SeedPrincipalAccountAsync(clientId);
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("200000002");

            var command = new AssignSecondaryAccountCommand { ClientId = clientId, InitialBalance = 1500m, CreatedByAdminId = "admin-1" };

            // Act
            var result = await _mediator.Send(command);

            // Assert
            var createdAccount = await _dbContext.SavingsAccounts.FirstAsync(a => a.AccountNumber == "200000002");
            createdAccount.Balance.Should().Be(1500m);
            var transactions = await _dbContext.Transactions.Where(t => t.SavingsAccountId == createdAccount.Id).ToListAsync();
            transactions.Should().ContainSingle();
            transactions[0].Category.Should().Be(TransactionCategory.AccountOpening);
            transactions[0].Amount.Should().Be(1500m);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_Conflict_When_AccountNumberGenerator_Fails()
        {
            // Arrange
            var clientId = "client-7";
            SetupActiveClient(clientId);
            await SeedPrincipalAccountAsync(clientId);
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ThrowsAsync(new InvalidOperationException());

            var command = new AssignSecondaryAccountCommand { ClientId = clientId, InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Principal_Account_Is_Inactive()
        {
            // Arrange
            var clientId = "client-8";
            SetupActiveClient(clientId);
            await SeedPrincipalAccountAsync(clientId, SavingsAccountStatus.Cancelled);

            var command = new AssignSecondaryAccountCommand { ClientId = clientId, InitialBalance = 0m };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The client must have an active principal savings account before a secondary account can be assigned.");
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
