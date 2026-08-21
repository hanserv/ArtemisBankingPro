using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;
using ArtemisBankingPro.Core.Application.DTOs.User;
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
    public class BeneficiaryServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly IMapper _mapper;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;

        private readonly BeneficiaryService _service;

        public BeneficiaryServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(BeneficiaryMappingConfig).Assembly);
            _mapper = new Mapper(config);

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();

            var beneficiaryRepository = new BeneficiaryRepository(_dbContext);
            var savingsAccountRepository = new SavingsAccountRepository(_dbContext);

            _service = new BeneficiaryService(
                beneficiaryRepository,
                savingsAccountRepository,
                _basicUserInfoServiceMock.Object,
                _mapper,
                NullLogger<BeneficiaryService>.Instance);
        }

        private async Task<SavingsAccount> SeedAccountAsync(string clientId, string accountNumber,
            SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = 1000m,
                Type = SavingsAccountType.Principal,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

        private async Task<Beneficiary> SeedBeneficiaryAsync(string clientId, int savingsAccountId, DateTime? createdAt = null)
        {
            var beneficiary = new Beneficiary
            {
                Id = 0,
                ClientId = clientId,
                SavingsAccountId = savingsAccountId,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            _dbContext.Beneficiaries.Add(beneficiary);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(beneficiary).State = EntityState.Detached;
            return beneficiary;
        }

        
        [Fact]
        public async Task GetByClientIdAsync_Should_Return_Empty_When_Client_Has_No_Beneficiaries()
        {
            // Act
            var result = await _service.GetByClientIdAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByClientIdAsync_Should_Return_Mapped_List_With_FullName_And_AccountNumber()
        {
            // Arrange
            var targetAccount = await SeedAccountAsync("owner-1", "300000001");
            await SeedBeneficiaryAsync("client-1", targetAccount.Id);
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("owner-1"))
                .ReturnsAsync(new UserBasicInfoDto { Id = "owner-1", Identification = "001", FullName = "Jane Owner", Email = "jane@test.com" });

            // Act
            var result = await _service.GetByClientIdAsync("client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].FullName.Should().Be("Jane Owner");
            result.Value[0].AccountNumber.Should().Be("300000001");
        }

        [Fact]
        public async Task GetByClientIdAsync_Should_Not_Include_Other_Clients_Beneficiaries()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000002");
            await SeedBeneficiaryAsync("client-1", account.Id);
            await SeedBeneficiaryAsync("other-client", account.Id);
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("owner-1"))
                .ReturnsAsync(new UserBasicInfoDto { Id = "owner-1", Identification = "001", FullName = "Jane Owner", Email = "jane@test.com" });

            // Act
            var result = await _service.GetByClientIdAsync("client-1");

            // Assert
            result.Value.Should().ContainSingle();
        }

        
        [Fact]
        public async Task GetByIdAsync_Should_Return_Failure_When_Not_Found_Or_Not_Owned_By_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000003");
            var beneficiary = await SeedBeneficiaryAsync("other-client", account.Id);

            // Act
            var result = await _service.GetByIdAsync(beneficiary.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected beneficiary does not exist.");
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Success_When_Found()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000004");
            var beneficiary = await SeedBeneficiaryAsync("client-1", account.Id);
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("owner-1"))
                .ReturnsAsync(new UserBasicInfoDto { Id = "owner-1", Identification = "001", FullName = "Jane Owner", Email = "jane@test.com" });

            // Act
            var result = await _service.GetByIdAsync(beneficiary.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.FullName.Should().Be("Jane Owner");
            result.Value.AccountNumber.Should().Be("300000004");
        }

        
        [Fact]
        public async Task AddAsync_Should_Return_Failure_When_AccountNumber_Is_Empty()
        {
            // Arrange
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number is required.");
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("12345678A")]
        [InlineData("1234567890")]
        public async Task AddAsync_Should_Return_Failure_When_AccountNumber_Is_Invalid(string accountNumber)
        {
            // Arrange
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = accountNumber };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number must contain exactly 9 digits.");
        }

        [Fact]
        public async Task AddAsync_Should_Return_Failure_When_Account_Does_Not_Exist()
        {
            // Arrange
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "999999999" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number entered does not correspond to a valid account.");
        }

        [Fact]
        public async Task AddAsync_Should_Return_Failure_When_Account_Is_Cancelled()
        {
            // Arrange
            await SeedAccountAsync("owner-1", "300000005", SavingsAccountStatus.Cancelled);
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "300000005" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You cannot add a cancelled account as a beneficiary.");
        }

        [Fact]
        public async Task AddAsync_Should_Return_Failure_When_Account_Belongs_To_Same_Client()
        {
            // Arrange
            await SeedAccountAsync("client-1", "300000006");
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "300000006" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You cannot add your own account as a beneficiary. Use the Transfer option to move funds between your accounts.");
        }

        [Fact]
        public async Task AddAsync_Should_Return_Failure_When_Beneficiary_Already_Exists()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000007");
            await SeedBeneficiaryAsync("client-1", account.Id);
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "300000007" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("This account is already registered as a beneficiary.");
        }

        [Fact]
        public async Task AddAsync_Should_Add_Beneficiary_When_Data_Is_Valid()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000008");
            var dto = new AddBeneficiaryDto { ClientId = "client-1", AccountNumber = "300000008" };

            // Act
            var result = await _service.AddAsync(dto);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var beneficiary = await _dbContext.Beneficiaries.SingleAsync();
            beneficiary.ClientId.Should().Be("client-1");
            beneficiary.SavingsAccountId.Should().Be(account.Id);
        }

        
        [Fact]
        public async Task DeleteAsync_Should_Return_Failure_When_Not_Found_Or_Not_Owned_By_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000009");
            var beneficiary = await SeedBeneficiaryAsync("other-client", account.Id);

            // Act
            var result = await _service.DeleteAsync(beneficiary.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected beneficiary does not exist.");
            (await _dbContext.Beneficiaries.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_Beneficiary_When_Owned_By_Client()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000010");
            var beneficiary = await SeedBeneficiaryAsync("client-1", account.Id);

            // Act
            var result = await _service.DeleteAsync(beneficiary.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            (await _dbContext.Beneficiaries.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task DeleteAsync_Should_Not_Delete_Other_Beneficiaries()
        {
            // Arrange
            var account = await SeedAccountAsync("owner-1", "300000011");
            var target = await SeedBeneficiaryAsync("client-1", account.Id);
            var otherAccount = await SeedAccountAsync("owner-2", "300000012");
            var untouched = await SeedBeneficiaryAsync("client-1", otherAccount.Id);

            // Act
            var result = await _service.DeleteAsync(target.Id, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            (await _dbContext.Beneficiaries.AnyAsync(b => b.Id == untouched.Id)).Should().BeTrue();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
