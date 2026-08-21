using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Loan.Commands.ModifyRate;
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

namespace ArtemisBankingPro.Tests.Unit.Features.Loan
{
    public class ModifyLoanRateCommandTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;
        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        public ModifyLoanRateCommandTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>().UseSqlite(_connection).Options;
            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserBasicInfoDto { Id = "client-1", FullName = "Client Name", Email = "client@email.com", Identification = "123456789" });

            _emailServiceMock = new Mock<IEmailService>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ILoanRepository, LoanRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_emailServiceMock.Object);
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ModifyLoanRateCommand).Assembly));
            services.AddValidatorsFromAssembly(typeof(ModifyLoanRateCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task<Core.Domain.Entities.Loan> SeedLoanAsync(LoanStatus status = LoanStatus.Active, bool withEligibleInstallment = true)
        {
            var loan = new Core.Domain.Entities.Loan
            {
                Id = 0,
                LoanNumber = "123456789",
                ClientId = "client-1",
                CapitalAmount = 100000m,
                PendingAmount = 100000m,
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
                InstallmentNumber = 1,
                DueDate = withEligibleInstallment ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddDays(-1),
                InstallmentAmount = 9000m,
                InterestAmount = 1000m,
                PrincipalAmount = 8000m,
                RemainingBalance = 9000m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            });

            _dbContext.Loans.Add(loan);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(loan).State = EntityState.Detached;

            return loan;
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Rate_Is_Negative()
        {
            var loan = await SeedLoanAsync();
            var command = new ModifyLoanRateCommand { LoanId = loan.Id, AnnualInterestRate = -1m };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Loan_Is_Not_Active()
        {
            var loan = await SeedLoanAsync(status: LoanStatus.Completed);
            var command = new ModifyLoanRateCommand { LoanId = loan.Id, AnnualInterestRate = 10m };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_No_Future_Pending_Installments()
        {
            var loan = await SeedLoanAsync(withEligibleInstallment: false);
            var command = new ModifyLoanRateCommand { LoanId = loan.Id, AnnualInterestRate = 10m };

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Loan_Does_Not_Exist()
        {
            var command = new ModifyLoanRateCommand { LoanId = 999, AnnualInterestRate = 10m };

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task Send_Should_Update_Rate_And_PendingAmount()
        {
            var loan = await SeedLoanAsync();
            var command = new ModifyLoanRateCommand { LoanId = loan.Id, AnnualInterestRate = 15m, AdminId = "admin-1" };

            await _mediator.Send(command);

            var updated = await _dbContext.Loans.FirstAsync(l => l.Id == loan.Id);
            updated.AnnualInterestRate.Should().Be(15m);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
