using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Features.SavingsAccount.Queries.GetAll;
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

namespace ArtemisBankingPro.Tests.Unit.Features.SavingsAccount
{
    public class GetAllSavingsAccountsQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetAllSavingsAccountsQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserBasicInfoDto { Id = "client-1", FullName = "Client Name", Email = "client@email.com", Identification = "001-1111111-1" });

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAllSavingsAccountsQuery).Assembly));
            services.AddValidatorsFromAssembly(typeof(GetAllSavingsAccountsQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.SavingsAccount> SeedAccountAsync(
            string clientId = "client-1", SavingsAccountStatus status = SavingsAccountStatus.Active,
            SavingsAccountType type = SavingsAccountType.Principal, DateTime? createdAt = null, decimal balance = 0m)
        {
            var account = new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = Random.Shared.Next(100000000, 999999999).ToString(),
                ClientId = clientId,
                Balance = balance,
                Type = type,
                Status = status,
                CreatedByAdminId = "admin-1",
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;

            return account;
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Page_Is_Invalid()
        {
            var query = new GetAllSavingsAccountsQuery { Page = 0, PageSize = 10 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PageSize_Is_Invalid()
        {
            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 0 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Default_To_Active_Accounts_Only()
        {
            await SeedAccountAsync(status: SavingsAccountStatus.Active);
            await SeedAccountAsync(status: SavingsAccountStatus.Cancelled);

            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 10 };

            var result = await _mediator.Send(query);

            result.TotalRecords.Should().Be(1);
            result.Items.Single().Status.Should().Be(SavingsAccountStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Filter_By_Type_When_Provided()
        {
            await SeedAccountAsync(type: SavingsAccountType.Principal);
            await SeedAccountAsync(type: SavingsAccountType.Secondary);

            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 10, Status = null, Type = SavingsAccountType.Secondary };

            var result = await _mediator.Send(query);

            result.TotalRecords.Should().Be(1);
            result.Items.Single().Type.Should().Be(SavingsAccountType.Secondary);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Result_When_Identification_Does_Not_Match_Any_Client()
        {
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync((string?)null);

            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            var result = await _mediator.Send(query);

            result.TotalRecords.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Filter_By_Client_Identification()
        {
            await SeedAccountAsync("client-1");
            await SeedAccountAsync("client-2");

            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-1111111-1")).ReturnsAsync("client-1");

            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 10, Identification = "001-1111111-1" };

            var result = await _mediator.Send(query);

            result.TotalRecords.Should().Be(1);
            result.Items.Single().ClientId.Should().Be("client-1");
        }

        [Fact]
        public async Task Send_Should_Respect_PageSize_Cap_Of_20()
        {
            for (var i = 0; i < 25; i++)
            {
                await SeedAccountAsync(clientId: $"client-{i}");
            }

            var query = new GetAllSavingsAccountsQuery { Page = 1, PageSize = 50 };

            var result = await _mediator.Send(query);

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
