using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
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
    public class CreditCardServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly IMapper _mapper;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        private readonly CreditCardService _service;

        public CreditCardServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(CreditCardDto).Assembly);
            _mapper = new Mapper(config);

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _emailServiceMock = new Mock<IEmailService>();

            var creditCardRepository = new CreditCardRepository(_dbContext);

            _service = new CreditCardService(
                creditCardRepository,
                _basicUserInfoServiceMock.Object,
                _mapper,
                _emailServiceMock.Object,
                NullLogger<CreditCardService>.Instance);
        }

        private async Task<CreditCard> SeedCreditCardAsync(
            string clientId = "client-1",
            string? cardNumber = null, // Permitir nulo para autogenerar
            decimal creditLimit = 10000m,
            decimal currentDebt = 0m,
            CreditCardStatus status = CreditCardStatus.Active,
            DateTime? createdAt = null)
        {
            var card = new CreditCard
            {
                Id = 0,
                CardNumber = cardNumber ?? Random.Shared.Next(10000000, 99999000).ToString() + Random.Shared.Next(10000000, 99999000).ToString(),
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = "12/28",
                CvcHash = "dummyhash",
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;

            return card;
        }


        [Fact]
        public async Task GetByIdAsync_Should_Return_Failure_When_Card_Not_Found()
        {
            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected credit card does not exist.");
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Success_With_ClientFullName_When_Found()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", creditLimit: 5000m);
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync("client-1")).ReturnsAsync("John Doe");

            // Act
            var result = await _service.GetByIdAsync(card.Id);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.ClientFullName.Should().Be("John Doe");
            result.Value.CreditLimit.Should().Be(5000m);
        }

        
        [Fact]
        public async Task AssignCreditCardAsync_Should_Return_Failure_When_CreditLimit_Is_Zero_Or_Less()
        {
            // Arrange
            var dto = new AssignCreditCardDto { ClientId = "client-1", CreditLimit = 0m };

            // Act
            var result = await _service.AssignCreditCardAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The credit limit must be greater than zero.");
            (await _dbContext.CreditCards.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task AssignCreditCardAsync_Should_Return_Failure_When_Client_Is_Not_Active()
        {
            // Arrange
            var dto = new AssignCreditCardDto { ClientId = "client-1", CreditLimit = 10000m };
            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(false);

            // Act
            var result = await _service.AssignCreditCardAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Credit Cards can only be assigned to active clients.");
        }

        [Fact]
        public async Task AssignCreditCardAsync_Should_Assign_Card_And_Send_Email_When_Validations_Pass()
        {
            // Arrange
            var dto = new AssignCreditCardDto { ClientId = "client-1", CreditLimit = 15000m };
            var userInfo = new UserBasicInfoDto { Id = "client-1", Identification = "000-0000000-0", FullName = "John Doe", Email = "john@doe.com" };

            _basicUserInfoServiceMock.Setup(s => s.IsClientActiveAsync("client-1")).ReturnsAsync(true);
            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("client-1")).ReturnsAsync(userInfo);

            // Act
            var result = await _service.AssignCreditCardAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var card = await _dbContext.CreditCards.SingleAsync();
            card.ClientId.Should().Be("client-1");
            card.CreditLimit.Should().Be(15000m);
            card.Status.Should().Be(CreditCardStatus.Active);
            card.CreatedByAdminId.Should().Be("admin-1");

            _emailServiceMock.Verify(email => email.SendAsync(It.Is<EmailRequestDto>(e => e.To == userInfo.Email)), Times.Once);
        }

        
        [Fact]
        public async Task ModifyCreditCardLimitAsync_Should_Return_Failure_When_Card_Is_Cancelled()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", status: CreditCardStatus.Cancelled);
            var dto = new ModifyCreditCardLimitDto { CreditCardId = card.Id, CreditLimit = 20000m };

            // Act
            var result = await _service.ModifyCreditCardLimitAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Cancelled credit cards cannot be modified.");
        }

        [Fact]
        public async Task ModifyCreditCardLimitAsync_Should_Return_Failure_When_New_Limit_Is_Lower_Than_Debt()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", currentDebt: 5000m, creditLimit: 10000m);
            var dto = new ModifyCreditCardLimitDto { CreditCardId = card.Id, CreditLimit = 4000m }; // Menor que 5000

            // Act
            var result = await _service.ModifyCreditCardLimitAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The credit limit cannot be lower than the current outstanding debt.");
        }

        [Fact]
        public async Task ModifyCreditCardLimitAsync_Should_Update_Limit_And_Send_Email_When_Valid()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", currentDebt: 2000m, creditLimit: 10000m);
            var dto = new ModifyCreditCardLimitDto { CreditCardId = card.Id, CreditLimit = 25000m };
            var userInfo = new UserBasicInfoDto { Id = "client-1", Identification = "000-0000000-0", FullName = "John Doe", Email = "john@doe.com" };

            _basicUserInfoServiceMock.Setup(s => s.GetBasicInfoAsync("client-1")).ReturnsAsync(userInfo);

            // Act
            var result = await _service.ModifyCreditCardLimitAsync(dto, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var updatedCard = await _dbContext.CreditCards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
            updatedCard.CreditLimit.Should().Be(25000m);

            _emailServiceMock.Verify(email => email.SendAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        }

       
        [Fact]
        public async Task CancelCreditCardAsync_Should_Return_Failure_When_Card_Has_Outstanding_Debt()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", currentDebt: 1500m);

            // Act
            var result = await _service.CancelCreditCardAsync(card.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("To cancel this card, the client must first settle the outstanding debt.");
        }

        [Fact]
        public async Task CancelCreditCardAsync_Should_Cancel_Card_Successfully_When_Debt_Is_Zero()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", currentDebt: 0m);

            // Act
            var result = await _service.CancelCreditCardAsync(card.Id, "admin-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var updatedCard = await _dbContext.CreditCards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
            updatedCard.Status.Should().Be(CreditCardStatus.Cancelled);
        }

       
        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_Page_Is_Invalid()
        {
            // Act
            var result = await _service.GetPagedAsync(new CreditCardFilterDto { Page = 0, PageSize = 10 });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The page parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_PageSize_Is_Invalid()
        {
            // Act
            var result = await _service.GetPagedAsync(new CreditCardFilterDto { Page = 1, PageSize = 0 });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The pageSize parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Return_Failure_When_Identification_Does_Not_Match_Any_Client()
        {
            // Arrange
            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-0000000-0")).ReturnsAsync((string?)null);
            var filter = new CreditCardFilterDto { Page = 1, PageSize = 10, Identification = "001-0000000-0" };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("There is no client registered with this identification.");
        }

        [Fact]
        public async Task GetPagedAsync_Should_Filter_By_Status_And_Client()
        {
            // Arrange
            await SeedCreditCardAsync("client-1", status: CreditCardStatus.Active);
            await SeedCreditCardAsync("client-1", status: CreditCardStatus.Cancelled);
            await SeedCreditCardAsync("client-2", status: CreditCardStatus.Active);

            _basicUserInfoServiceMock.Setup(s => s.GetUserIdByIdentificationAsync("001-1111111-1")).ReturnsAsync("client-1");
            _basicUserInfoServiceMock.Setup(s => s.GetFullNameAsync(It.IsAny<string>())).ReturnsAsync("Client Name");

            var filter = new CreditCardFilterDto
            {
                Page = 1,
                PageSize = 10,
                Status = CreditCardStatus.Active,
                Identification = "001-1111111-1"
            };

            // Act
            var result = await _service.GetPagedAsync(filter);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRecords.Should().Be(1);
            result.Value!.Items.First().ClientId.Should().Be("client-1");
            result.Value!.Items.First().Status.Should().Be(CreditCardStatus.Active);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
