using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.HermesPay.Queries.GetCommerceTransactions;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
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
    public class GetCommerceTransactionsQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetCommerceTransactionsQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ICommerceRepository, CommerceRepository>();
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);

            services.AddValidatorsFromAssembly(typeof(GetCommerceTransactionsQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCommerceTransactionsQuery).Assembly));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
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

        private async Task<Core.Domain.Entities.SavingsAccount> SeedPrincipalAccountAsync(string clientId)
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = $"1{Random.Shared.Next(10000000, 99999999)}",
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

        private async Task SeedTransactionAsync(int accountId, decimal amount, TransactionCategory category, string origin = "1234", DateTime? createdAt = null)
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Id = 0,
                SavingsAccountId = accountId,
                Amount = amount,
                Type = TransactionType.Credit,
                Category = category,
                Origin = origin,
                Beneficiary = "commerce",
                Status = TransactionStatus.Approved,
                CreatedAt = createdAt ?? DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Page_Is_Zero()
        {
            var query = new GetCommerceTransactionsQuery { CommerceId = 1, Page = 0 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The page parameter must be greater than zero.");
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PageSize_Is_Zero()
        {
            var query = new GetCommerceTransactionsQuery { CommerceId = 1, PageSize = 0 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ArtemisBankingPro.Core.Application.Exceptions.ValidationException>();
            exception.Which.Errors.Should().Contain("The pageSize parameter must be greater than zero.");
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Commerce_Does_Not_Exist()
        {
            var query = new GetCommerceTransactionsQuery { CommerceId = 999 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_BadRequest_When_Commerce_Is_Inactive()
        {
            // Arrange
            var commerce = await SeedCommerceAsync(isActive: false);

            // Act
            var act = async () => await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Result_When_Commerce_Has_No_Associated_User()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync((string?)null);

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            result.Items.Should().BeEmpty();
            result.TotalRecords.Should().Be(0);
            result.CommerceName.Should().Be(commerce.Name);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Result_When_Associated_User_Has_No_Principal_Account()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("user-without-account");

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            result.Items.Should().BeEmpty();
            result.TotalRecords.Should().Be(0);
        }

        [Fact]
        public async Task Send_Should_Return_Only_HermesPayment_Transactions()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("user-1");
            var account = await SeedPrincipalAccountAsync("user-1");

            await SeedTransactionAsync(account.Id, 100m, TransactionCategory.HermesPayment);
            await SeedTransactionAsync(account.Id, 200m, TransactionCategory.Deposit);

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            result.Items.Should().ContainSingle();
            result.Items[0].Amount.Should().Be(100m);
        }

        [Fact]
        public async Task Send_Should_Return_Transactions_Ordered_By_Most_Recent_First()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("user-2");
            var account = await SeedPrincipalAccountAsync("user-2");

            await SeedTransactionAsync(account.Id, 100m, TransactionCategory.HermesPayment, createdAt: DateTime.UtcNow.AddDays(-2));
            await SeedTransactionAsync(account.Id, 200m, TransactionCategory.HermesPayment, createdAt: DateTime.UtcNow.AddDays(-1));
            await SeedTransactionAsync(account.Id, 300m, TransactionCategory.HermesPayment, createdAt: DateTime.UtcNow);

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            result.Items.Select(t => t.Amount).Should().ContainInOrder(300m, 200m, 100m);
        }

        [Fact]
        public async Task Send_Should_Map_CardLastFourDigits_From_Origin()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("user-3");
            var account = await SeedPrincipalAccountAsync("user-3");

            await SeedTransactionAsync(account.Id, 100m, TransactionCategory.HermesPayment, origin: "5678");

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id });

            // Assert
            result.Items.Single().CardLastFourDigits.Should().Be("5678");
        }

        [Fact]
        public async Task Send_Should_Cap_PageSize_At_Twenty_When_Requested_Higher()
        {
            // Arrange
            var commerce = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce.Id)).ReturnsAsync("user-4");
            var account = await SeedPrincipalAccountAsync("user-4");

            for (var i = 0; i < 25; i++)
            {
                await SeedTransactionAsync(account.Id, 10m + i, TransactionCategory.HermesPayment);
            }

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce.Id, PageSize = 50 });

            // Assert
            result.PageSize.Should().Be(20);
            result.Items.Should().HaveCount(20);
            result.TotalRecords.Should().Be(25);
        }

        [Fact]
        public async Task Send_Should_Not_Include_Transactions_From_Other_Commerces()
        {
            // Arrange
            var commerce1 = await SeedCommerceAsync();
            var commerce2 = await SeedCommerceAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce1.Id)).ReturnsAsync("user-5");
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByCommerceIdAsync(commerce2.Id)).ReturnsAsync("user-6");
            var account1 = await SeedPrincipalAccountAsync("user-5");
            var account2 = await SeedPrincipalAccountAsync("user-6");

            await SeedTransactionAsync(account1.Id, 100m, TransactionCategory.HermesPayment);
            await SeedTransactionAsync(account2.Id, 999m, TransactionCategory.HermesPayment);

            // Act
            var result = await _mediator.Send(new GetCommerceTransactionsQuery { CommerceId = commerce1.Id });

            // Assert
            result.Items.Should().ContainSingle();
            result.Items[0].Amount.Should().Be(100m);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
