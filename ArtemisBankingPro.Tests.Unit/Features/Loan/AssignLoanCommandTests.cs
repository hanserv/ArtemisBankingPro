using System.Net;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Exceptions;
using ArtemisBankingPro.Core.Application.Features.Loan.Commands.Asign;
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

namespace ArtemisBankingPro.Tests.Unit.Features.Loan
{
    public class AssignLoanCommandTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly ServiceProvider _provider;
        private readonly IMediator _mediator;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IFinancialSummaryService> _financialSummaryServiceMock;
        private readonly Mock<ILoanNumberGenerator> _loanNumberGeneratorMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        private const string ClientId = "client-1";

        public AssignLoanCommandTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _financialSummaryServiceMock = new Mock<IFinancialSummaryService>();
            _loanNumberGeneratorMock = new Mock<ILoanNumberGenerator>();
            _emailServiceMock = new Mock<IEmailService>();

            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync(ClientId)).ReturnsAsync(true);
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(ClientId))
                .ReturnsAsync(new UserBasicInfoDto { Id = ClientId, FullName = "Client Name", Email = "client@email.com",Identification = "123456789" });
            _financialSummaryServiceMock.Setup(s => s.CheckIfHighRiskAsync(ClientId, It.IsAny<decimal>()))
                .ReturnsAsync((LoanRiskWarningDto?)null);
            _loanNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("123456789");

            var config = new TypeAdapterConfig();
            config.Scan(typeof(LoanDto).Assembly);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(_dbContext);
            services.AddScoped<ILoanRepository, LoanRepository>();
            services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddSingleton(_basicUserInfoServiceMock.Object);
            services.AddSingleton(_financialSummaryServiceMock.Object);
            services.AddSingleton(_loanNumberGeneratorMock.Object);
            services.AddSingleton(_emailServiceMock.Object);
            services.AddSingleton(_unitOfWorkMock.Object);
            services.AddSingleton(config);
            services.AddScoped<IMapper, Mapper>();

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssignLoanCommand).Assembly));
            services.AddValidatorsFromAssembly(typeof(AssignLoanCommand).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            _provider = services.BuildServiceProvider();
            _mediator = _provider.GetRequiredService<IMediator>();
        }

        private async Task SeedPrincipalAccountAsync(string clientId = ClientId, SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            _dbContext.SavingsAccounts.Add(new Core.Domain.Entities.SavingsAccount
            {
                Id = 0,
                AccountNumber = "111111111",
                ClientId = clientId,
                Balance = 0m,
                Type = SavingsAccountType.Principal,
                Status = status,
                CreatedByAdminId = "admin-1",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        private static AssignLoanCommand ValidCommand() => new()
        {
            ClientId = ClientId,
            CapitalAmount = 100000m,
            AnnualInterestRate = 12m,
            TermInMonths = 12,
            AdminId = "admin-1"
        };

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_CapitalAmount_Is_Not_Greater_Than_Zero()
        {
            var command = ValidCommand();
            command.CapitalAmount = 0;

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_TermInMonths_Is_Not_Allowed()
        {
            var command = ValidCommand();
            command.TermInMonths = 10;

            var act = async () => await _mediator.Send(command);

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Client_Is_Not_Active()
        {
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync(ClientId)).ReturnsAsync(false);
            await SeedPrincipalAccountAsync();

            var act = async () => await _mediator.Send(ValidCommand());

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Client_Already_Has_An_Active_Loan()
        {
            await SeedPrincipalAccountAsync();
            _dbContext.Loans.Add(new Core.Domain.Entities.Loan
            {
                Id = 0,
                LoanNumber = "999999999",
                ClientId = ClientId,
                CapitalAmount = 50000m,
                PendingAmount = 50000m,
                AnnualInterestRate = 10m,
                TermInMonths = 12,
                CreatedByAdminId = "admin-1",
                Status = LoanStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            var act = async () => await _mediator.Send(ValidCommand());

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ValidationException_When_Client_Has_No_Active_Principal_Account()
        {
            var act = async () => await _mediator.Send(ValidCommand());

            await act.Should().ThrowAsync<Core.Application.Exceptions.ValidationException>();
        }

        [Fact]
        public async Task Send_Should_Throw_ApiException_With_NotFound_When_Client_Does_Not_Exist_In_Handler()
        {
            await SeedPrincipalAccountAsync();
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync(ClientId)).ReturnsAsync((UserBasicInfoDto?)null);

            var act = async () => await _mediator.Send(ValidCommand());

            var exception = await act.Should().ThrowAsync<ApiException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Send_Should_Throw_HighRiskLoanException_When_Client_Is_High_Risk_And_Not_Confirmed()
        {
            await SeedPrincipalAccountAsync();
            _financialSummaryServiceMock.Setup(s => s.CheckIfHighRiskAsync(ClientId, It.IsAny<decimal>()))
                .ReturnsAsync(new LoanRiskWarningDto
                {
                    RiskType = RiskType.CurrentHighRisk,
                    CurrentDebt = 60000m,
                    ProjectedDebt = 160000m,
                    AverageDebt = 50000m
                });

            var command = ValidCommand();
            command.ConfirmHighRisk = false;

            var act = async () => await _mediator.Send(command);

            var exception = await act.Should().ThrowAsync<HighRiskLoanException>();
            exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
            exception.Which.RiskType.Should().Be(RiskType.CurrentHighRisk.ToString());
        }

        [Fact]
        public async Task Send_Should_Create_Loan_When_Client_Is_High_Risk_But_Confirmed()
        {
            await SeedPrincipalAccountAsync();
            _financialSummaryServiceMock.Setup(s => s.CheckIfHighRiskAsync(ClientId, It.IsAny<decimal>()))
                .ReturnsAsync(new LoanRiskWarningDto
                {
                    RiskType = RiskType.ProjectedHighRisk,
                    CurrentDebt = 10000m,
                    ProjectedDebt = 60000m,
                    AverageDebt = 50000m
                });

            var command = ValidCommand();
            command.ConfirmHighRisk = true;

            var result = await _mediator.Send(command);

            result.Status.Should().Be(LoanStatus.Active);
        }

        [Fact]
        public async Task Send_Should_Disburse_Capital_To_Principal_Account_And_Register_Transaction_With_LoanNumber_As_Origin()
        {
            await SeedPrincipalAccountAsync();

            var result = await _mediator.Send(ValidCommand());

            var account = await _dbContext.SavingsAccounts.FirstAsync(a => a.ClientId == ClientId);
            account.Balance.Should().Be(100000m);

            var transaction = await _dbContext.Transactions.FirstAsync(t => t.SavingsAccountId == account.Id);
            transaction.Type.Should().Be(TransactionType.Credit);
            transaction.Origin.Should().Be(result.LoanNumber);
            transaction.Origin.Should().Be("123456789");
        }

        [Fact]
        public async Task Send_Should_Create_Installments_Matching_The_Requested_Term()
        {
            await SeedPrincipalAccountAsync();

            var result = await _mediator.Send(ValidCommand());

            var loan = await _dbContext.Loans.Include(l => l.Installments).FirstAsync(l => l.Id == result.Id);
            loan.Installments.Should().HaveCount(12);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
