using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Tests.Integration.Persistence.Repositories
{
    public class LoanRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly LoanRepository _repository;

        public LoanRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _repository = new LoanRepository(_dbContext);
        }

        private async Task<Loan> CreateLoanAsync(string loanNumber, string clientId, LoanStatus status = LoanStatus.Active)
        {
            var loan = new Loan
            {
                Id = 0,
                LoanNumber = loanNumber,
                ClientId = clientId,
                CapitalAmount = 10000m,
                PendingAmount = 10000m,
                AnnualInterestRate = 12m,
                TermInMonths = 12,
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Loans.Add(loan);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(loan).State = EntityState.Detached;
            return loan;
        }

        [Fact]
        public async Task AddAsync_Should_Add_Loan_To_Database()
        {
            // Arrange
            var loan = new Loan
            {
                Id = 0,
                LoanNumber = "LN-000001",
                ClientId = "client-1",
                CapitalAmount = 5000m,
                PendingAmount = 5000m,
                AnnualInterestRate = 10m,
                TermInMonths = 6,
                CreatedByAdminId = "admin-1",
                Status = LoanStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.AddAsync(loan);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            var loans = await _dbContext.Loans.ToListAsync();
            loans.Should().ContainSingle();
        }

        [Fact]
        public async Task AddAsync_Should_Not_Persist_Loan_When_Not_Called()
        {
            // Arrange & Act
            var loans = await _dbContext.Loans.ToListAsync();

            // Assert
            loans.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Loan_When_Exists()
        {
            // Arrange
            var loan = await CreateLoanAsync("LN-000002", "client-2");

            // Act
            var result = await _repository.GetByIdAsync(loan.Id);

            // Assert
            result.Should().NotBeNull();
            result!.LoanNumber.Should().Be("LN-000002");
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_When_NotExists()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_Should_Modify_Existing_Loan()
        {
            // Arrange
            var loan = await CreateLoanAsync("LN-000003", "client-3");

            var tracked = await _dbContext.Loans.FindAsync(loan.Id);
            tracked!.Status = LoanStatus.Completed;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var updated = await _dbContext.Loans.AsNoTracking().FirstAsync(l => l.Id == loan.Id);
            updated.Status.Should().Be(LoanStatus.Completed);
        }

        [Fact]
        public async Task UpdateAsync_Should_Not_Modify_Other_Loans()
        {
            // Arrange
            var target = await CreateLoanAsync("LN-000004", "client-4");
            var untouched = await CreateLoanAsync("LN-000005", "client-4");

            var tracked = await _dbContext.Loans.FindAsync(target.Id);
            tracked!.Status = LoanStatus.Completed;
            _dbContext.Entry(tracked).State = EntityState.Detached;

            // Act
            await _repository.UpdateAsync(tracked);

            // Assert
            var stillActive = await _dbContext.Loans.AsNoTracking().FirstAsync(l => l.Id == untouched.Id);
            stillActive.Status.Should().Be(LoanStatus.Active);
        }

        [Fact]
        public async Task MarkOverdueInstallmentsAsync_Should_Mark_Pending_Overdue_Installments_As_Late()
        {
            // Arrange
            var loan = await CreateLoanAsync("LN-000006", "client-5");
            var overdueInstallment = new LoanInstallment
            {
                Id = 0,
                LoanId = loan.Id,
                InstallmentNumber = 1,
                DueDate = DateTime.UtcNow.Date.AddDays(-5),
                InstallmentAmount = 500m,
                InterestAmount = 50m,
                PrincipalAmount = 450m,
                RemainingBalance = 9550m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            };
            _dbContext.LoanInstallments.Add(overdueInstallment);
            await _dbContext.SaveChangesAsync();

            // Act
            var affectedRows = await _repository.MarkOverdueInstallmentsAsync();

            // Assert
            affectedRows.Should().Be(1);
            var updated = await _dbContext.LoanInstallments.AsNoTracking().FirstAsync(i => i.Id == overdueInstallment.Id);
            updated.IsLate.Should().BeTrue();
        }

        [Fact]
        public async Task MarkOverdueInstallmentsAsync_Should_Not_Mark_Paid_Or_FutureDue_Installments()
        {
            // Arrange
            var loan = await CreateLoanAsync("LN-000007", "client-6");
            var paidOverdue = new LoanInstallment
            {
                Id = 0,
                LoanId = loan.Id,
                InstallmentNumber = 1,
                DueDate = DateTime.UtcNow.Date.AddDays(-5),
                InstallmentAmount = 500m,
                InterestAmount = 50m,
                PrincipalAmount = 450m,
                RemainingBalance = 0m,
                Status = InstallmentStatus.Paid,
                IsLate = false
            };
            var futureInstallment = new LoanInstallment
            {
                Id = 0,
                LoanId = loan.Id,
                InstallmentNumber = 2,
                DueDate = DateTime.UtcNow.Date.AddDays(10),
                InstallmentAmount = 500m,
                InterestAmount = 45m,
                PrincipalAmount = 455m,
                RemainingBalance = 9095m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            };
            _dbContext.LoanInstallments.AddRange(paidOverdue, futureInstallment);
            await _dbContext.SaveChangesAsync();

            // Act
            var affectedRows = await _repository.MarkOverdueInstallmentsAsync();

            // Assert
            affectedRows.Should().Be(0);
            var stillPaid = await _dbContext.LoanInstallments.AsNoTracking().FirstAsync(i => i.Id == paidOverdue.Id);
            var stillFuture = await _dbContext.LoanInstallments.AsNoTracking().FirstAsync(i => i.Id == futureInstallment.Id);
            stillPaid.IsLate.Should().BeFalse();
            stillFuture.IsLate.Should().BeFalse();
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Return_Active_Loans_With_Installments_Ordered_By_Most_Recent()
        {
            // Arrange
            var clientId = "client-7";
            var older = await CreateLoanAsync("LN-000008", clientId);
            var newer = await CreateLoanAsync("LN-000009", clientId);

            // ajustamos CreatedAt manualmente para garantizar el orden, ya que ambos se crean casi al mismo instante
            var trackedOlder = await _dbContext.Loans.FindAsync(older.Id);
            trackedOlder!.CreatedAt = DateTime.UtcNow.AddDays(-3);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(trackedOlder).State = EntityState.Detached;

            _dbContext.LoanInstallments.Add(new LoanInstallment
            {
                Id = 0,
                LoanId = newer.Id,
                InstallmentNumber = 1,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                InstallmentAmount = 500m,
                InterestAmount = 50m,
                PrincipalAmount = 450m,
                RemainingBalance = 9550m,
                Status = InstallmentStatus.Pending,
                IsLate = false
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(newer.Id);
            result[0].Installments.Should().ContainSingle();
            result[1].Id.Should().Be(older.Id);
        }

        [Fact]
        public async Task GetActiveByClientIdAsync_Should_Not_Include_Completed_Loans_Or_Other_Clients()
        {
            // Arrange
            var clientId = "client-8";
            var activeLoan = await CreateLoanAsync("LN-000010", clientId, LoanStatus.Active);
            var completedLoan = await CreateLoanAsync("LN-000011", clientId, LoanStatus.Completed);
            var otherClientLoan = await CreateLoanAsync("LN-000012", "other-client", LoanStatus.Active);

            // Act
            var result = await _repository.GetActiveByClientIdAsync(clientId);

            // Assert
            result.Should().ContainSingle();
            result.Should().NotContain(l => l.Id == completedLoan.Id);
            result.Should().NotContain(l => l.Id == otherClientLoan.Id);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
