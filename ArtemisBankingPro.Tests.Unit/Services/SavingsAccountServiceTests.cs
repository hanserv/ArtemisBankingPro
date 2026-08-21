using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos;
using ArtemisBankingPro.Core.Application.Services;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArtemisBankingPro.Tests.Unit.Services
{
    public class SavingsAccountServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly IMapper _mapper;

        private readonly Mock<IAccountNumberGenerator> _accountNumberGeneratorMock;
        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IFinancialSummaryService> _financialSummaryServiceMock;

        private readonly SavingsAccountService _service;

        public SavingsAccountServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(SavingsAccountMappingConfig).Assembly);
            _mapper = new Mapper(config);

            _accountNumberGeneratorMock = new Mock<IAccountNumberGenerator>();
            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _financialSummaryServiceMock = new Mock<IFinancialSummaryService>();

            var savingsAccountRepository = new SavingsAccountRepository(_dbContext);
            var transactionRepository = new TransactionRepository(_dbContext);
            var unitOfWork = new UnitOfWork(_dbContext); 

            _service = new SavingsAccountService(
                savingsAccountRepository,
                transactionRepository,
                _accountNumberGeneratorMock.Object,
                unitOfWork,
                _basicUserInfoServiceMock.Object,
                _mapper,
                _financialSummaryServiceMock.Object,
                NullLogger<SavingsAccountService>.Instance);
        }

        private async Task<SavingsAccount> SeedAccountAsync(string clientId, SavingsAccountType type = SavingsAccountType.Principal,
            SavingsAccountStatus status = SavingsAccountStatus.Active, decimal balance = 0m,
            DateTime? createdAt = null)
        {
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = $"ACC-{Guid.NewGuid():N}"[..12],
                ClientId = clientId,
                Balance = balance,
                Type = type,
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

      
        [Fact]
        public async Task GetByIdAsync_Should_Return_Failure_When_Account_Not_Found()
        {
            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account does not exist.");
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Success_With_ClientFullName_When_Found()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", balance: 500m);
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync("client-1")).ReturnsAsync("John Doe");

            // Act
            var result = await _service.GetByIdAsync(account.Id);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.ClientFullName.Should().Be("John Doe");
            result.Value.Balance.Should().Be(500m);
        }

       
        [Fact]
        public async Task GetActiveAccountsByClientIdAsync_Should_Return_Mapped_List()
        {
            // Arrange
            await SeedAccountAsync("client-1", SavingsAccountType.Principal);
            await SeedAccountAsync("client-1", SavingsAccountType.Secondary);
            await SeedAccountAsync("other-client", SavingsAccountType.Principal);

            // Act
            var result = await _service.GetActiveAccountsByClientIdAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
        }

       
        [Fact]
        public async Task CreatePrincipalAccountAsync_Should_Return_Failure_When_InitialAmount_Is_Negative()
        {
            // Act
            var result = await _service.CreatePrincipalAccountAsync("client-1", -10m);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The initial amount cannot be negative.");
            (await _dbContext.SavingsAccounts.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task CreatePrincipalAccountAsync_Should_Add_Account_And_Transaction_When_InitialAmount_Is_Positive()
        {
            // Arrange
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("100000999");

            // Act
            var result = await _service.CreatePrincipalAccountAsync("client-1", 300m);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var account = await _dbContext.SavingsAccounts.SingleAsync();
            account.ClientId.Should().Be("client-1");
            account.Balance.Should().Be(300m);
            account.Type.Should().Be(SavingsAccountType.Principal);

            var transaction = await _dbContext.Transactions.SingleAsync();
            transaction.Category.Should().Be(TransactionCategory.AccountOpening);
            transaction.Amount.Should().Be(300m);
        }

        [Fact]
        public async Task CreatePrincipalAccountAsync_Should_Not_Add_Transaction_When_InitialAmount_Is_Zero()
        {
            // Arrange
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("100000998");

            // Act
            var result = await _service.CreatePrincipalAccountAsync("client-1", 0m);

            // Assert
            result.IsSuccess.Should().BeTrue();
            (await _dbContext.SavingsAccounts.CountAsync()).Should().Be(1);
            (await _dbContext.Transactions.CountAsync()).Should().Be(0);
        }

        
        [Fact]
        public async Task CreditAdditionalAmountAsync_Should_Return_Failure_When_No_Principal_Account()
        {
            // Act
            var result = await _service.CreditAdditionalAmountAsync("client-1", 100m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The client does not have a principal savings account.");
        }

        [Fact]
        public async Task CreditAdditionalAmountAsync_Should_Update_Balance_And_Add_Transaction()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", SavingsAccountType.Principal, balance: 200m);

            // Act
            var result = await _service.CreditAdditionalAmountAsync("client-1", 50m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updated = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            updated.Balance.Should().Be(250m);

            var transaction = await _dbContext.Transactions.SingleAsync();
            transaction.Category.Should().Be(TransactionCategory.AdministrativeAdjustment);
            transaction.Amount.Should().Be(50m);
            transaction.PerformedByUserId.Should().Be("admin-1");
        }

      
        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_Page_Is_Invalid()
        {
            // Act
            var result = await _service.GetPagedAsync(new SavingsAccountFilterDto { Page = 0, PageSize = 10 });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The page parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_PageSize_Is_Invalid()
        {
            // Act
            var result = await _service.GetPagedAsync(new SavingsAccountFilterDto { Page = 1, PageSize = 0 });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The pageSize parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Clamp_PageSize_To_Twenty()
        {
            // Arrange
            for (var i = 0; i < 5; i++)
            {
                await SeedAccountAsync("client-x");
            }
            var filter = new SavingsAccountFilterDto { Page = 1, PageSize = 50 };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeTrue();
            filter.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_Identification_Does_Not_Match_Any_Client()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync((string?)null);
            var filter = new SavingsAccountFilterDto { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("There is no client registered with this identification.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_Client_Has_No_Accounts()
        {
            // Arrange
            await SeedAccountAsync("other-client");
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync("client-1");
            var filter = new SavingsAccountFilterDto { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("This client has no savings accounts registered.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Filter_By_Status_And_Type()
        {
            // Arrange
            await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active);
            await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Cancelled);
            await SeedAccountAsync("client-2", SavingsAccountType.Principal, SavingsAccountStatus.Active);
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var filter = new SavingsAccountFilterDto
            {
                Page = 1,
                PageSize = 10,
                Status = SavingsAccountStatus.Active,
                Type = SavingsAccountType.Principal
            };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRecords.Should().Be(2);
        }

        [Fact]
        public async Task GetPagedAsync_Should_Order_Active_Before_Cancelled_When_Filtering_By_Client_Without_Status()
        {
            // Arrange
            var older = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Cancelled, createdAt: DateTime.UtcNow.AddDays(-5));
            var newerCancelled = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Cancelled, createdAt: DateTime.UtcNow.AddDays(-1));
            var active = await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active, createdAt: DateTime.UtcNow.AddDays(-10));

            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-1111111-1")).ReturnsAsync("client-1");
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync("client-1")).ReturnsAsync("Client Name");

            var filter = new SavingsAccountFilterDto { Page = 1, PageSize = 10, Identification = "001-1111111-1" };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.Items.Select(i => i.Id).Should().ContainInOrder(active.Id, newerCancelled.Id, older.Id);
        }

      
        [Fact]
        public async Task ValidateClientForAssignmentAsync_Should_Return_Failure_When_ClientId_Is_Empty()
        {
            // Act
            var result = await _service.ValidateClientForAssignmentAsync("  ");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You must select a client to continue.");
        }

        [Fact]
        public async Task ValidateClientForAssignmentAsync_Should_Return_Failure_When_Client_Is_Not_Active()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(false);

            // Act
            var result = await _service.ValidateClientForAssignmentAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Savings accounts can only be assigned to active clients.");
        }

        [Fact]
        public async Task ValidateClientForAssignmentAsync_Should_Return_Failure_When_No_Active_Principal_Account()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(true);

            // Act
            var result = await _service.ValidateClientForAssignmentAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The client must have an active principal savings account before a secondary account can be assigned.");
        }

        [Fact]
        public async Task ValidateClientForAssignmentAsync_Should_Return_Success_When_Client_And_Principal_Account_Are_Valid()
        {
            // Arrange
            await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active);
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(true);

            // Act
            var result = await _service.ValidateClientForAssignmentAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

       
        [Fact]
        public async Task CreateSecondaryAccountAsync_Should_Return_Failure_When_InitialBalance_Is_Negative()
        {
            // Act
            var result = await _service.CreateSecondaryAccountAsync("client-1", -5m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The initial balance cannot be negative.");
        }

        [Fact]
        public async Task CreateSecondaryAccountAsync_Should_Return_Validation_Error_When_Client_Not_Eligible()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(false);

            // Act
            var result = await _service.CreateSecondaryAccountAsync("client-1", 100m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Savings accounts can only be assigned to active clients.");
            (await _dbContext.SavingsAccounts.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task CreateSecondaryAccountAsync_Should_Add_Account_And_Transaction_When_InitialBalance_Is_Positive()
        {
            // Arrange
            await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active);
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(true);
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("200000999");

            // Act
            var result = await _service.CreateSecondaryAccountAsync("client-1", 150m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var secondary = await _dbContext.SavingsAccounts.SingleAsync(a => a.Type == SavingsAccountType.Secondary);
            secondary.Balance.Should().Be(150m);
            secondary.CreatedByAdminId.Should().Be("admin-1");

            var transaction = await _dbContext.Transactions.SingleAsync();
            transaction.Category.Should().Be(TransactionCategory.AccountOpening);
            transaction.Amount.Should().Be(150m);
        }

        [Fact]
        public async Task CreateSecondaryAccountAsync_Should_Not_Add_Transaction_When_InitialBalance_Is_Zero()
        {
            // Arrange
            await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active);
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(true);
            _accountNumberGeneratorMock.Setup(g => g.GenerateAsync()).ReturnsAsync("200000998");

            // Act
            var result = await _service.CreateSecondaryAccountAsync("client-1", 0m, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            (await _dbContext.SavingsAccounts.CountAsync(a => a.Type == SavingsAccountType.Secondary)).Should().Be(1);
            (await _dbContext.Transactions.CountAsync()).Should().Be(0);
        }

        
        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Return_Failure_When_Account_Not_Found()
        {
            // Act
            var result = await _service.CancelSecondaryAccountAsync(999, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account does not exist.");
        }

        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Return_Failure_When_Account_Already_Cancelled()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Cancelled);

            // Act
            var result = await _service.CancelSecondaryAccountAsync(account.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account is already cancelled.");
        }

        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Return_Failure_When_Account_Is_Principal()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active);

            // Act
            var result = await _service.CancelSecondaryAccountAsync(account.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Principal accounts cannot be cancelled.");
        }

        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Return_Failure_When_No_Principal_Account_To_Receive_Funds()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Active, balance: 100m);

            // Act
            var result = await _service.CancelSecondaryAccountAsync(account.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("It is not possible to cancel the account because the client does not have an active principal account to receive the funds.");
        }

        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Transfer_Balance_And_Cancel_When_Balance_Is_Positive()
        {
            // Arrange
            var secondary = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Active, balance: 300m);
            var principal = await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active, balance: 100m);

            // Act
            var result = await _service.CancelSecondaryAccountAsync(secondary.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var updatedSecondary = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == secondary.Id);
            updatedSecondary.Balance.Should().Be(0m);
            updatedSecondary.Status.Should().Be(SavingsAccountStatus.Cancelled);

            var updatedPrincipal = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == principal.Id);
            updatedPrincipal.Balance.Should().Be(400m);

            (await _dbContext.Transactions.CountAsync()).Should().Be(2);
        }

        [Fact]
        public async Task CancelSecondaryAccountAsync_Should_Cancel_Without_Transfer_When_Balance_Is_Zero()
        {
            // Arrange
            var secondary = await SeedAccountAsync("client-1", SavingsAccountType.Secondary, SavingsAccountStatus.Active, balance: 0m);
            var principal = await SeedAccountAsync("client-1", SavingsAccountType.Principal, SavingsAccountStatus.Active, balance: 100m);

            // Act
            var result = await _service.CancelSecondaryAccountAsync(secondary.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var updatedSecondary = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == secondary.Id);
            updatedSecondary.Status.Should().Be(SavingsAccountStatus.Cancelled);

            var updatedPrincipal = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == principal.Id);
            updatedPrincipal.Balance.Should().Be(100m);

            (await _dbContext.Transactions.CountAsync()).Should().Be(0);
        }

       
        [Fact]
        public async Task GetClientAccountByIdAsync_Should_Return_Failure_When_Not_Found_Or_Not_Owned_By_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("other-client");

            // Act
            var result = await _service.GetClientAccountByIdAsync(account.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account does not exist.");
        }

        [Fact]
        public async Task GetClientAccountByIdAsync_Should_Return_Success_When_Found()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", balance: 750m);

            // Act
            var result = await _service.GetClientAccountByIdAsync(account.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.Balance.Should().Be(750m);
        }

        
        [Fact]
        public async Task GetClientAccountTransactionsAsync_Should_Return_Failure_When_Account_Not_Owned_By_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("other-client");

            // Act
            var result = await _service.GetClientAccountTransactionsAsync(account.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account does not exist.");
        }

        [Fact]
        public async Task GetClientAccountTransactionsAsync_Should_Return_Mapped_Transactions_When_Account_Belongs_To_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1");
            _dbContext.Transactions.Add(new Transaction
            {
                Id = 0,
                SavingsAccountId = account.Id,
                Amount = 100m,
                Type = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Origin = "Cashier",
                Beneficiary = "client-1",
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.GetClientAccountTransactionsAsync(account.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
