using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetAll;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
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

namespace ArtemisBankingPro.Tests.Unit.Features.Loan
{
    public class GetAllLoansQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetAllLoansQueryHandlerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(LoanDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ILoanRepository, LoanRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAllLoansQuery).Assembly));
            services.AddValidatorsFromAssembly(typeof(GetAllLoansQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Loan> SeedLoanAsync(string clientId = "client-1", LoanStatus status = LoanStatus.Active,
            decimal capitalAmount = 50000m, DateTime? createdAt = null,
            int installmentCount = 3)
        {
            var loan = new Core.Domain.Entities.Loan
            {
                Id = 0,
                LoanNumber = Random.Shared.Next(100000000, 999999999).ToString(),
                ClientId = clientId,
                CapitalAmount = capitalAmount,
                PendingAmount = capitalAmount,
                AnnualInterestRate = 12m,
                TermInMonths = 12,
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            for (var i = 1; i <= installmentCount; i++)
            {
                loan.Installments.Add(new LoanInstallment
                {
                    Id = 0,
                    LoanId = 0, 
                    InstallmentNumber = i,
                    DueDate = DateTime.UtcNow.AddMonths(i),
                    InstallmentAmount = 4500m,
                    InterestAmount = 500m,
                    PrincipalAmount = 4000m,
                    RemainingBalance = 4500m,
                    Status = InstallmentStatus.Pending,
                    IsLate = false
                });
            }

            _dbContext.Loans.Add(loan);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(loan).State = EntityState.Detached;

            return loan;
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Page_Is_Invalid()
        {
            var query = new GetAllLoansQuery { Page = 0, PageSize = 10 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_PageSize_Is_Invalid()
        {
            var query = new GetAllLoansQuery { Page = 1, PageSize = 0 };

            var act = async () => await _mediator.Send(query);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Default_To_Active_Loans_Only()
        {
            // Arrange
            await SeedLoanAsync(status: LoanStatus.Active);
            await SeedLoanAsync(status: LoanStatus.Completed);

            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetAllLoansQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(1);
            result.Items.Single().Status.Should().Be(LoanStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Return_Empty_Result_When_Identification_Does_Not_Match_Any_Client()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync((string?)null);

            var query = new GetAllLoansQuery { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Send_Should_Filter_By_Identification_And_Include_All_Statuses_When_Status_Is_Null()
        {
            // Arrange
            await SeedLoanAsync("client-1", status: LoanStatus.Active, createdAt: DateTime.UtcNow.AddDays(-2));
            await SeedLoanAsync("client-1", status: LoanStatus.Completed, createdAt: DateTime.UtcNow.AddDays(-1));
            await SeedLoanAsync("client-2", status: LoanStatus.Active);

            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-1111111-1")).ReturnsAsync("client-1");
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetAllLoansQuery { Page = 1, PageSize = 10, Status = null, Identification = "001-1111111-1" };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.TotalRecords.Should().Be(2);
            result.Items.All(i => i.ClientId == "client-1").Should().BeTrue();
            result.Items.First().Status.Should().Be(LoanStatus.Active); 
        }

        [Fact]
        public async Task Send_Should_Map_MonthlyInstallment_And_PaidInstallments_From_Loaded_Installments()
        {
            // Arrange
            await SeedLoanAsync(installmentCount: 3);
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetAllLoansQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            var dto = result.Items.Single();
            dto.TotalInstallments.Should().Be(3);
            dto.MonthlyInstallment.Should().Be(4500m);
            dto.PaidInstallments.Should().Be(0);
        }

        [Fact]
        public async Task Send_Should_Respect_PageSize_Cap_Of_20()
        {
            // Arrange
            for (var i = 0; i < 25; i++)
            {
                await SeedLoanAsync(clientId: $"client-{i}");
            }

            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetAllLoansQuery { Page = 1, PageSize = 50 };

            // Act
            var result = await _mediator.Send(query);

            // Assert
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
