using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
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
    public class TransactionServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;
        private readonly IMapper _mapper;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        private readonly TransactionService _service;

        public TransactionServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            var config = new TypeAdapterConfig();
            config.Scan(typeof(TransactionMappingConfig).Assembly);
            _mapper = new Mapper(config);

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _emailServiceMock = new Mock<IEmailService>();

            var transactionRepository = new TransactionRepository(_dbContext);
            var savingsAccountRepository = new SavingsAccountRepository(_dbContext);
            var creditCardRepository = new CreditCardRepository(_dbContext);
            var unitOfWork = new UnitOfWork(_dbContext);

            _service = new TransactionService(transactionRepository, savingsAccountRepository,
                _mapper, _basicUserInfoServiceMock.Object,
                _emailServiceMock.Object, unitOfWork,
                NullLogger<TransactionService>.Instance, creditCardRepository);

            _basicUserInfoServiceMock
                .Setup(s => s.GetBasicInfoAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => new UserBasicInfoDto { Id = id, Identification = "001", FullName = "Client " + id, Email = $"{id}@test.com" });

            _emailServiceMock
                .Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>()))
                .ReturnsAsync(Result.Success());
        }

        private async Task<SavingsAccount> SeedAccountAsync(
            string clientId,
            string accountNumber,
            decimal balance = 1000m,
            SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = balance,
                Type = SavingsAccountType.Principal,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

        private async Task<CreditCard> SeedCreditCardAsync(string clientId, string cardNumber,
            decimal creditLimit = 5000m, decimal currentDebt = 0m,
            CreditCardStatus status = CreditCardStatus.Active)
        {
            var card = new CreditCard
            {
                Id = 0,
                CardNumber = cardNumber,
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = "12/30",
                CvcHash = "hashed-cvc",
                CreatedByAdminId = "admin-1",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.CreditCards.Add(card);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(card).State = EntityState.Detached;
            return card;
        }

        
        [Fact]
        public async Task GetAccountTransactionsAsync_Should_Return_Failure_When_Page_Is_Invalid()
        {
            // Act
            var result = await _service.GetAccountTransactionsAsync(1, 0, 10);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The page parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetAccountTransactionsAsync_Should_Return_Failure_When_PageSize_Is_Invalid()
        {
            // Act
            var result = await _service.GetAccountTransactionsAsync(1, 1, 0);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The pageSize parameter must be greater than zero.");
        }

        [Fact]
        public async Task GetAccountTransactionsAsync_Should_Clamp_PageSize_To_Twenty()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", "500000001");

            // Act
            var result = await _service.GetAccountTransactionsAsync(account.Id, 1, 50);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task GetAccountTransactionsAsync_Should_Return_Failure_When_Account_Not_Found()
        {
            // Act
            var result = await _service.GetAccountTransactionsAsync(999, 1, 10);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected account does not exist.");
        }

        [Fact]
        public async Task GetAccountTransactionsAsync_Should_Return_Only_Transactions_For_That_Account_Ordered_By_Most_Recent()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", "500000002");
            var otherAccount = await SeedAccountAsync("client-2", "500000003");

            _dbContext.Transactions.AddRange(
                new Transaction { Id = 0, SavingsAccountId = account.Id, Amount = 100m, Type = TransactionType.Credit, Category = TransactionCategory.Deposit, Origin = "DEPOSIT", Beneficiary = account.AccountNumber, Status = TransactionStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new Transaction { Id = 0, SavingsAccountId = account.Id, Amount = 200m, Type = TransactionType.Debit, Category = TransactionCategory.Withdrawal, Origin = account.AccountNumber, Beneficiary = "WITHDRAWAL", Status = TransactionStatus.Approved, CreatedAt = DateTime.UtcNow },
                new Transaction { Id = 0, SavingsAccountId = otherAccount.Id, Amount = 999m, Type = TransactionType.Credit, Category = TransactionCategory.Deposit, Origin = "DEPOSIT", Beneficiary = otherAccount.AccountNumber, Status = TransactionStatus.Approved, CreatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.GetAccountTransactionsAsync(account.Id, 1, 10);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRecords.Should().Be(2);
            result.Value.Items.Should().HaveCount(2);
            result.Value.Items[0].Amount.Should().Be(200m); // más reciente primero
        }

        
        [Fact]
        public async Task ValidateDepositAsync_Should_Return_Failure_When_Amount_Is_Zero_Or_Negative()
        {
            // Arrange
            var dto = new DepositDto { AccountNumber = "500000004", Amount = 0m };

            // Act
            var result = await _service.ValidateDepositAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The deposit amount must be greater than zero.");
        }

        [Fact]
        public async Task ValidateDepositAsync_Should_Return_Failure_When_Account_Not_Found_Or_Inactive()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000005", status: SavingsAccountStatus.Cancelled);
            var dto = new DepositDto { AccountNumber = "500000005", Amount = 100m };

            // Act
            var result = await _service.ValidateDepositAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number entered does not correspond to a valid account.");
        }

        [Fact]
        public async Task ValidateDepositAsync_Should_Return_Success_With_Confirmation_Data()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000006");
            var dto = new DepositDto { AccountNumber = "500000006", Amount = 250m };

            // Act
            var result = await _service.ValidateDepositAsync(dto);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.AccountHolderName.Should().Be("Client client-1");
            result.Value.Amount.Should().Be(250m);
        }

       
        [Fact]
        public async Task ConfirmDepositAsync_Should_Increase_Balance_And_Create_Transaction()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", "500000007", balance: 100m);
            var dto = new DepositConfirmationDto { AccountNumber = "500000007", AccountHolderName = "Client client-1", Amount = 50m };

            // Act
            var result = await _service.ConfirmDepositAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updated = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            updated.Balance.Should().Be(150m);
            var transaction = await _dbContext.Transactions.SingleAsync();
            transaction.Category.Should().Be(TransactionCategory.Deposit);
            transaction.PerformedByUserId.Should().Be("cashier-1");
        }

        [Fact]
        public async Task ConfirmDepositAsync_Should_Return_Success_With_Warning_When_Email_Fails()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000008");
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>())).ReturnsAsync(Result.Failure("SMTP down"));
            var dto = new DepositConfirmationDto { AccountNumber = "500000008", AccountHolderName = "Client client-1", Amount = 50m };

            // Act
            var result = await _service.ConfirmDepositAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Be("The deposit was completed successfully, but the notification email could not be sent.");
        }

        
        [Fact]
        public async Task ValidateWithdrawalAsync_Should_Log_Rejected_When_Insufficient_Balance()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000009", balance: 20m);
            var dto = new WithdrawalDto { AccountNumber = "500000009", Amount = 100m };

            // Act
            var result = await _service.ValidateWithdrawalAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount entered exceeds the account's available balance.");
            var rejected = await _dbContext.Transactions.SingleAsync();
            rejected.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task ValidateWithdrawalAsync_Should_Return_Success_When_Valid()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000010", balance: 500m);
            var dto = new WithdrawalDto { AccountNumber = "500000010", Amount = 100m };

            // Act
            var result = await _service.ValidateWithdrawalAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.AccountHolderName.Should().Be("Client client-1");
        }

        
        [Fact]
        public async Task ConfirmWithdrawalAsync_Should_Decrease_Balance_And_Create_Transaction()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", "500000011", balance: 500m);
            var dto = new WithdrawalConfirmationDto { AccountNumber = "500000011", AccountHolderName = "Client client-1", Amount = 100m };

            // Act
            var result = await _service.ConfirmWithdrawalAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updated = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            updated.Balance.Should().Be(400m);
        }

        [Fact]
        public async Task ConfirmWithdrawalAsync_Should_Return_Failure_When_Insufficient_Balance()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000012", balance: 10m);
            var dto = new WithdrawalConfirmationDto { AccountNumber = "500000012", AccountHolderName = "Client client-1", Amount = 100m };

            // Act
            var result = await _service.ConfirmWithdrawalAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount entered exceeds the account's available balance.");
        }

        
        [Fact]
        public async Task ValidateCreditCardPaymentAsync_Should_Return_Failure_When_Account_Not_Found()
        {
            // Arrange
            var dto = new CreditCardPaymentDto { SourceAccountNumber = "000000000", CardNumber = "4000000000000001", Amount = 100m };

            // Act
            var result = await _service.ValidateCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number entered does not correspond to a valid account.");
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("400000000000000A")]
        public async Task ValidateCreditCardPaymentAsync_Should_Return_Failure_When_CardNumber_Is_Invalid(string cardNumber)
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000013");
            var dto = new CreditCardPaymentDto { SourceAccountNumber = "500000013", CardNumber = cardNumber, Amount = 100m };

            // Act
            var result = await _service.ValidateCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The card number must contain 16 digits.");
        }

        [Fact]
        public async Task ValidateCreditCardPaymentAsync_Should_Return_Failure_When_Card_Not_Found_Or_Inactive()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000014");
            var dto = new CreditCardPaymentDto { SourceAccountNumber = "500000014", CardNumber = "4000000000000099", Amount = 100m };

            // Act
            var result = await _service.ValidateCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The card number entered does not correspond to a valid card.");
        }

        [Fact]
        public async Task ValidateCreditCardPaymentAsync_Should_Log_Rejected_When_Card_Has_No_Debt()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000015");
            await SeedCreditCardAsync("client-1", "4000000000000002", currentDebt: 0m);
            var dto = new CreditCardPaymentDto { SourceAccountNumber = "500000015", CardNumber = "4000000000000002", Amount = 100m };

            // Act
            var result = await _service.ValidateCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected card has no pending debt.");
            (await _dbContext.Transactions.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task ValidateCreditCardPaymentAsync_Should_Cap_EffectiveAmount_At_CurrentDebt()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000016", balance: 500m);
            await SeedCreditCardAsync("client-2", "4000000000000003", currentDebt: 80m);
            var dto = new CreditCardPaymentDto { SourceAccountNumber = "500000016", CardNumber = "4000000000000003", Amount = 200m };

            // Act
            var result = await _service.ValidateCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.EnteredAmount.Should().Be(200m);
            result.Value.EffectiveAmount.Should().Be(80m);
            result.Value.CardHolderName.Should().Be("Client client-2");
        }

        
        [Fact]
        public async Task ConfirmCreditCardPaymentAsync_Should_Update_Balance_And_Debt_When_Valid()
        {
            // Arrange
            var account = await SeedAccountAsync("client-1", "500000017", balance: 500m);
            var card = await SeedCreditCardAsync("client-1", "4000000000000004", currentDebt: 100m);
            var dto = new CreditCardPaymentConfirmationDto
            {
                SourceAccountNumber = "500000017",
                AccountHolderName = "Client client-1",
                CardNumber = "4000000000000004",
                CardLastFourDigits = "0004",
                CardHolderName = "Client client-1",
                EnteredAmount = 100m,
                EffectiveAmount = 100m
            };

            // Act
            var result = await _service.ConfirmCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updatedAccount = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            var updatedCard = await _dbContext.CreditCards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
            updatedAccount.Balance.Should().Be(400m);
            updatedCard.CurrentDebt.Should().Be(0m);
        }

        [Fact]
        public async Task ConfirmCreditCardPaymentAsync_Should_Return_Failure_When_Insufficient_Balance()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000018", balance: 10m);
            await SeedCreditCardAsync("client-1", "4000000000000005", currentDebt: 100m);
            var dto = new CreditCardPaymentConfirmationDto
            {
                SourceAccountNumber = "500000018",
                AccountHolderName = "Client client-1",
                CardNumber = "4000000000000005",
                CardLastFourDigits = "0005",
                CardHolderName = "Client client-1",
                EnteredAmount = 100m,
                EffectiveAmount = 100m
            };

            // Act
            var result = await _service.ConfirmCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount entered exceeds the account's available balance.");
        }

        [Fact]
        public async Task ConfirmCreditCardPaymentAsync_Should_Send_Only_One_Email_When_Same_Client_Owns_Account_And_Card()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000019", balance: 500m);
            await SeedCreditCardAsync("client-1", "4000000000000006", currentDebt: 50m);
            var dto = new CreditCardPaymentConfirmationDto
            {
                SourceAccountNumber = "500000019",
                AccountHolderName = "Client client-1",
                CardNumber = "4000000000000006",
                CardLastFourDigits = "0006",
                CardHolderName = "Client client-1",
                EnteredAmount = 50m,
                EffectiveAmount = 50m
            };

            // Act
            var result = await _service.ConfirmCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            _emailServiceMock.Verify(s => s.SendAsync(It.IsAny<EmailRequestDto>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmCreditCardPaymentAsync_Should_Send_Two_Emails_When_Different_Clients_Own_Account_And_Card()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000020", balance: 500m);
            await SeedCreditCardAsync("client-2", "4000000000000007", currentDebt: 50m);
            var dto = new CreditCardPaymentConfirmationDto
            {
                SourceAccountNumber = "500000020",
                AccountHolderName = "Client client-1",
                CardNumber = "4000000000000007",
                CardLastFourDigits = "0007",
                CardHolderName = "Client client-2",
                EnteredAmount = 50m,
                EffectiveAmount = 50m
            };

            // Act
            var result = await _service.ConfirmCreditCardPaymentAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            _emailServiceMock.Verify(s => s.SendAsync(It.IsAny<EmailRequestDto>()), Times.Exactly(2));
        }

       
        [Fact]
        public async Task ValidateThirdPartyTransactionAsync_Should_Return_Failure_When_Source_Account_Not_Found()
        {
            // Arrange
            var dto = new ThirdPartyTransactionDto { SourceAccountNumber = "000000000", DestinationAccountNumber = "500000021", Amount = 100m };
            await SeedAccountAsync("client-1", "500000021");

            // Act
            var result = await _service.ValidateThirdPartyTransactionAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The source account number entered does not correspond to a valid account.");
        }

        [Fact]
        public async Task ValidateThirdPartyTransactionAsync_Should_Log_Rejected_When_Source_Inactive()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000022", status: SavingsAccountStatus.Cancelled);
            await SeedAccountAsync("client-2", "500000023");
            var dto = new ThirdPartyTransactionDto { SourceAccountNumber = "500000022", DestinationAccountNumber = "500000023", Amount = 100m };

            // Act
            var result = await _service.ValidateThirdPartyTransactionAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The source account number entered does not correspond to a valid account.");
            var rejected = await _dbContext.Transactions.SingleAsync();
            rejected.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task ValidateThirdPartyTransactionAsync_Should_Return_Success_When_Valid()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000024", balance: 500m);
            await SeedAccountAsync("client-2", "500000025");
            var dto = new ThirdPartyTransactionDto { SourceAccountNumber = "500000024", DestinationAccountNumber = "500000025", Amount = 100m };

            // Act
            var result = await _service.ValidateThirdPartyTransactionAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.SourceAccountHolderName.Should().Be("Client client-1");
            result.Value.DestinationAccountHolderName.Should().Be("Client client-2");
        }

       
        [Fact]
        public async Task ConfirmThirdPartyTransactionAsync_Should_Move_Balances_When_Valid()
        {
            // Arrange
            var source = await SeedAccountAsync("client-1", "500000026", balance: 500m);
            var destination = await SeedAccountAsync("client-2", "500000027", balance: 50m);
            var dto = new ThirdPartyTransactionConfirmationDto
            {
                SourceAccountNumber = "500000026",
                SourceAccountHolderName = "Client client-1",
                DestinationAccountNumber = "500000027",
                DestinationAccountHolderName = "Client client-2",
                Amount = 100m
            };

            // Act
            var result = await _service.ConfirmThirdPartyTransactionAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updatedSource = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == source.Id);
            var updatedDestination = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == destination.Id);
            updatedSource.Balance.Should().Be(400m);
            updatedDestination.Balance.Should().Be(150m);
            (await _dbContext.Transactions.CountAsync()).Should().Be(2);
        }

        [Fact]
        public async Task ConfirmThirdPartyTransactionAsync_Should_Return_Failure_When_Same_Account()
        {
            // Arrange
            await SeedAccountAsync("client-1", "500000028", balance: 500m);
            var dto = new ThirdPartyTransactionConfirmationDto
            {
                SourceAccountNumber = "500000028",
                SourceAccountHolderName = "Client client-1",
                DestinationAccountNumber = "500000028",
                DestinationAccountHolderName = "Client client-1",
                Amount = 100m
            };

            // Act
            var result = await _service.ConfirmThirdPartyTransactionAsync(dto, "cashier-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The source account and the destination account cannot be the same.");
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
