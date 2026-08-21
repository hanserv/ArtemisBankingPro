using System.Net;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetTransactions;
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

namespace ArtemisBankingPro.Tests.Unit.Features.SavingsAccount
{
    public class GetSavingsAccountTransactionsQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetSavingsAccountTransactionsQueryHandlerTests()
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
            config.Scan(typeof(TransactionDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSavingsAccountTransactionsQuery).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.SavingsAccount> SeedAccountAsync(string accountNumber, string clientId = "client-1")
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = 1000m,
                Type = SavingsAccountType.Principal,
                Status = SavingsAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;

            return account;
        }

        private async Task SeedTransactionAsync(int accountId, decimal amount, DateTime? createdAt = null)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Id = 0,
                SavingsAccountId = accountId,
                Amount = amount,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-1",
                Status = TransactionStatus.Approved,
                CreatedAt = createdAt ?? DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Account_Does_Not_Exist()
        {
            var query = new GetSavingsAccountTransactionsQuery { AccountNumber = "000000000" };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Return_Account_Details_With_Client_Name_Resolved()
        {
            // Arrange
            var account = await SeedAccountAsync("100000001");

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery { AccountNumber = account.AccountNumber });

            // Assert
            result.AccountNumber.Should().Be(account.AccountNumber);
            result.ClientFullName.Should().Be("Client Name");
            result.Balance.Should().Be(account.Balance);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Transactions_When_Account_Has_None()
        {
            // Arrange
            var account = await SeedAccountAsync("100000002");

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery { AccountNumber = account.AccountNumber });

            // Assert
            result.Transactions.Items.Should().BeEmpty();
            result.Transactions.TotalRecords.Should().Be(0);
        }

        [Fact]
        public async Task Send_Should_Return_Transactions_Ordered_By_Most_Recent_First()
        {
            // Arrange
            var account = await SeedAccountAsync("100000003");
            await SeedTransactionAsync(account.Id, 100m, DateTime.UtcNow.AddDays(-2));
            await SeedTransactionAsync(account.Id, 200m, DateTime.UtcNow.AddDays(-1));
            await SeedTransactionAsync(account.Id, 300m, DateTime.UtcNow);

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery { AccountNumber = account.AccountNumber });

            // Assert
            result.Transactions.Items.Select(t => t.Amount).Should().ContainInOrder(300m, 200m, 100m);
        }

        [Fact]
        public async Task Send_Should_Not_Include_Transactions_From_Other_Accounts()
        {
            // Arrange
            var account1 = await SeedAccountAsync("100000004", "client-1");
            var account2 = await SeedAccountAsync("100000005", "client-2");

            await SeedTransactionAsync(account1.Id, 100m);
            await SeedTransactionAsync(account2.Id, 200m);

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery { AccountNumber = account1.AccountNumber });

            // Assert
            result.Transactions.Items.Should().ContainSingle();
            result.Transactions.Items.Single().Amount.Should().Be(100m);
        }

        [Fact]
        public async Task Send_Should_Cap_PageSize_At_Twenty_When_Requested_Higher()
        {
            // Arrange
            var account = await SeedAccountAsync("100000006");
            for (var i = 0; i < 25; i++)
            {
                await SeedTransactionAsync(account.Id, 10m + i);
            }

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery
            {
                AccountNumber = account.AccountNumber,
                PageSize = 50
            });

            // Assert
            result.Transactions.PageSize.Should().Be(20);
            result.Transactions.Items.Should().HaveCount(20);
            result.Transactions.TotalRecords.Should().Be(25);
        }

        [Fact]
        public async Task Send_Should_Return_Second_Page_With_Remaining_Transactions()
        {
            // Arrange
            var account = await SeedAccountAsync("100000007");
            for (var i = 0; i < 25; i++)
            {
                await SeedTransactionAsync(account.Id, 10m + i, DateTime.UtcNow.AddMinutes(-i));
            }

            // Act
            var result = await _mediator.Send(new GetSavingsAccountTransactionsQuery
            {
                AccountNumber = account.AccountNumber,
                Page = 2,
                PageSize = 20
            });

            // Assert
            result.Transactions.Items.Should().HaveCount(5);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
