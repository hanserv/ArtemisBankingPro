using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Loan.Queries.GetById;
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
    public class GetLoanByIdQueryHandlerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        public GetLoanByIdQueryHandlerTests()
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

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetLoanByIdQuery).Assembly));
            services.AddValidatorsFromAssembly(typeof(GetLoanByIdQuery).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Loan> SeedLoanAsync(string clientId = "client-1", LoanStatus status = LoanStatus.Active)
        {
            var loan = new Core.Domain.Entities.Loan
            {
                Id = 0,
                LoanNumber = Random.Shared.Next(100000000, 999999999).ToString(),
                ClientId = clientId,
                CapitalAmount = 50000m,
                PendingAmount = 50000m,
                AnnualInterestRate = 12m,
                TermInMonths = 12,
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            loan.Installments.Add(new LoanInstallment
            {
                Id = 0,
                LoanId = 0,
                InstallmentNumber = 3,
                DueDate = DateTime.UtcNow.AddMonths(3),
                InstallmentAmount = 4500m,
                InterestAmount = 500m,
                PrincipalAmount = 4000m,
                RemainingBalance = 4500m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            });

            loan.Installments.Add(new LoanInstallment
            {
                Id = 0,
                LoanId = 0,
                InstallmentNumber = 1,
                DueDate = DateTime.UtcNow.AddMonths(1),
                InstallmentAmount = 4500m,
                InterestAmount = 500m,
                PrincipalAmount = 4000m,
                RemainingBalance = 4500m,
                Status = InstallmentStatus.Paid,
                IsLate = false
            });

            loan.Installments.Add(new LoanInstallment
            {
                Id = 0,
                LoanId = 0,
                InstallmentNumber = 2,
                DueDate = DateTime.UtcNow.AddMonths(2),
                InstallmentAmount = 4500m,
                InterestAmount = 500m,
                PrincipalAmount = 4000m,
                RemainingBalance = 4500m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            });

            _dbContext.Loans.Add(loan);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(loan).State = EntityState.Detached;

            return loan;
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Loan_Does_Not_Exist()
        {
            var query = new GetLoanByIdQuery { Id = 999 };

            var act = async () => await _mediator.Send(query);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Return_Loan_With_ClientFullName_When_Loan_Exists()
        {
            // Arrange
            var loan = await SeedLoanAsync(clientId: "client-1");
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync("client-1")).ReturnsAsync("Client Name");

            var query = new GetLoanByIdQuery { Id = loan.Id };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.Loan.Id.Should().Be(loan.Id);
            result.Loan.ClientFullName.Should().Be("Client Name");
        }

        [Fact]
        public async Task Send_Should_Return_Installments_Ordered_By_InstallmentNumber()
        {
            // Arrange
            var loan = await SeedLoanAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetLoanByIdQuery { Id = loan.Id };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.Installments.Should().HaveCount(3);
            result.Installments.Select(i => i.InstallmentNumber).Should().ContainInOrder(1, 2, 3);
        }

        [Fact]
        public async Task Send_Should_Map_Installment_Statuses_Correctly()
        {
            // Arrange
            var loan = await SeedLoanAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var query = new GetLoanByIdQuery { Id = loan.Id };

            // Act
            var result = await _mediator.Send(query);

            // Assert
            result.Installments.First(i => i.InstallmentNumber == 1).PaymentStatus.Should().Be(InstallmentStatus.Paid);
            result.Installments.First(i => i.InstallmentNumber == 2).PaymentStatus.Should().Be(InstallmentStatus.Pending);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
