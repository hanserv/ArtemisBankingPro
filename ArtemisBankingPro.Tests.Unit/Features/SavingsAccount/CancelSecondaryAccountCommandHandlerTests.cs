using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Commands.CancelSecondary;
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

namespace ArtemisBankingPro.Tests.Unit.Features.SavingsAccount
{
    public class CancelSecondaryAccountCommandHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        public CancelSecondaryAccountCommandHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddValidatorsFromAssembly(typeof(CancelSecondaryAccountCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CancelSecondaryAccountCommand).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.SavingsAccount> SeedAccountAsync(
            string accountNumber, string clientId, SavingsAccountType type,
            SavingsAccountStatus status = SavingsAccountStatus.Active, decimal balance = 0m)
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = balance,
                Type = type,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;

            return account;
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Account_Does_Not_Exist()
        {
            var command = new CancelSecondaryAccountCommand { AccountNumber = "000000000" };

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_BadRequest_When_Account_Already_Cancelled()
        {
            // Arrange
            var account = await SeedAccountAsync("200000001", "client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Cancelled);

            var command = new CancelSecondaryAccountCommand { AccountNumber = account.AccountNumber };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_BadRequest_When_Account_Is_Principal()
        {
            // Arrange
            var account = await SeedAccountAsync("100000001", "client-2", SavingsAccountType.Principal);

            var command = new CancelSecondaryAccountCommand { AccountNumber = account.AccountNumber };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
            exception.Which.Message.Should().Be("Principal accounts cannot be cancelled.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_When_Client_Has_No_Active_Principal_Account()
        {
            // Arrange
            var account = await SeedAccountAsync("200000002", "client-3", SavingsAccountType.Secondary, balance: 500m);

            var command = new CancelSecondaryAccountCommand { AccountNumber = account.AccountNumber };

            // Act
            var act = async () => await _mediator.Send(command);

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

     

        [Fact]
        public async Task Send_Should_Cancel_Account_And_Transfer_Balance_To_Principal_Account()
        {
            // Arrange
            var clientId = "client-5";
            var principal = await SeedAccountAsync("100000003", clientId, SavingsAccountType.Principal, balance: 1000m);
            var secondary = await SeedAccountAsync("200000004", clientId, SavingsAccountType.Secondary, balance: 500m);

            var command = new CancelSecondaryAccountCommand { AccountNumber = secondary.AccountNumber, PerformedByAdminId = "admin-1" };

            // Act
            await _mediator.Send(command);

            // Assert
            var updatedSecondary = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == secondary.Id);
            var updatedPrincipal = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == principal.Id);

            updatedSecondary.Status.Should().Be(SavingsAccountStatus.Cancelled);
            updatedSecondary.Balance.Should().Be(0m);
            updatedPrincipal.Balance.Should().Be(1500m);
        }

        [Fact]
        public async Task Send_Should_Create_Two_Transfer_Transactions_When_Balance_Is_Transferred()
        {
            // Arrange
            var clientId = "client-6";
            var principal = await SeedAccountAsync("100000004", clientId, SavingsAccountType.Principal, balance: 200m);
            var secondary = await SeedAccountAsync("200000005", clientId, SavingsAccountType.Secondary, balance: 700m);

            var command = new CancelSecondaryAccountCommand { AccountNumber = secondary.AccountNumber, PerformedByAdminId = "admin-1" };

            // Act
            await _mediator.Send(command);

            // Assert
            var secondaryTransactions = await _dbContext.Transactions.Where(t => t.SavingsAccountId == secondary.Id).ToListAsync();
            var principalTransactions = await _dbContext.Transactions.Where(t => t.SavingsAccountId == principal.Id).ToListAsync();

            secondaryTransactions.Should().ContainSingle();
            secondaryTransactions[0].Type.Should().Be(TransactionType.Debit);
            secondaryTransactions[0].Amount.Should().Be(700m);
            secondaryTransactions[0].Category.Should().Be(TransactionCategory.Transfer);

            principalTransactions.Should().ContainSingle();
            principalTransactions[0].Type.Should().Be(TransactionType.Credit);
            principalTransactions[0].Amount.Should().Be(700m);
        }

        [Fact]
        public async Task Send_Should_Cancel_Account_Without_Creating_Transactions_When_Balance_Is_Zero()
        {
            // Arrange
            var clientId = "client-7";
            var principal = await SeedAccountAsync("100000005", clientId, SavingsAccountType.Principal, balance: 1000m);
            var secondary = await SeedAccountAsync("200000006", clientId, SavingsAccountType.Secondary, balance: 0m);

            var command = new CancelSecondaryAccountCommand { AccountNumber = secondary.AccountNumber, PerformedByAdminId = "admin-1" };

            // Act
            await _mediator.Send(command);

            // Assert
            var updatedSecondary = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == secondary.Id);
            var updatedPrincipal = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == principal.Id);

            updatedSecondary.Status.Should().Be(SavingsAccountStatus.Cancelled);
            updatedPrincipal.Balance.Should().Be(1000m); // sin cambios

            var allTransactions = await _dbContext.Transactions.ToListAsync();
            allTransactions.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Not_Affect_Other_Clients_Accounts()
        {
            // Arrange
            var clientId = "client-8";
            await SeedAccountAsync("100000006", clientId, SavingsAccountType.Principal, balance: 1000m);
            var secondary = await SeedAccountAsync("200000007", clientId, SavingsAccountType.Secondary, balance: 400m);

            var otherClientAccount = await SeedAccountAsync("100000007", "other-client", SavingsAccountType.Principal, balance: 5000m);

            var command = new CancelSecondaryAccountCommand { AccountNumber = secondary.AccountNumber, PerformedByAdminId = "admin-1" };

            // Act
            await _mediator.Send(command);

            // Assert
            var untouched = await _dbContext.SavingsAccounts.AsNoTracking().FirstAsync(a => a.Id == otherClientAccount.Id);
            untouched.Balance.Should().Be(5000m);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
