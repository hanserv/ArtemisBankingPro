using ArtemisBankingPro.Core.Application;
using ArtemisBankingPro.Core.Application.DTOs.Email;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.Services;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;
using ArtemisBankingPro.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArtemisBankingPro.Tests.Unit.Services
{
    public class ClientTransactionServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ArtemisBankingProContext _dbContext;

        private readonly Mock<IBasicUserInfoService> _basicUserInfoServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        private readonly ClientTransactionService _service;

        public ClientTransactionServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ArtemisBankingProContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new ArtemisBankingProContext(options);
            _dbContext.Database.EnsureCreated();

            _basicUserInfoServiceMock = new Mock<IBasicUserInfoService>();
            _emailServiceMock = new Mock<IEmailService>();

            var savingsAccountRepository = new SavingsAccountRepository(_dbContext);
            var transactionRepository = new TransactionRepository(_dbContext);
            var creditCardRepository = new CreditCardRepository(_dbContext);
            var beneficiaryRepository = new BeneficiaryRepository(_dbContext);
            var cardConsumptionRepository = new CardConsumptionRepository(_dbContext);
            var unitOfWork = new UnitOfWork(_dbContext);

            _service = new ClientTransactionService(
                savingsAccountRepository,
                transactionRepository,
                _basicUserInfoServiceMock.Object,
                _emailServiceMock.Object,
                unitOfWork,
                NullLogger<ClientTransactionService>.Instance,
                creditCardRepository,
                beneficiaryRepository,
                cardConsumptionRepository);

            _basicUserInfoServiceMock
                .Setup(s => s.GetBasicInfoAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => new UserBasicInfoDto { Id = id, Identification = "001", FullName = "Client " + id, Email = $"{id}@test.com" });

            _emailServiceMock
                .Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>()))
                .ReturnsAsync(Result.Success());
        }

        private async Task<SavingsAccount> SeedAccountAsync(string clientId,string accountNumber,
            decimal balance = 1000m, SavingsAccountType type = SavingsAccountType.Principal,
            SavingsAccountStatus status = SavingsAccountStatus.Active)
        {
            var account = new SavingsAccount
            {
                Id = 0,
                AccountNumber = accountNumber,
                ClientId = clientId,
                Balance = balance,
                Type = type,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.SavingsAccounts.Add(account);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(account).State = EntityState.Detached;
            return account;
        }

        private async Task<CreditCard> SeedCreditCardAsync(string clientId,string cardNumber,
            decimal creditLimit = 5000m, decimal currentDebt = 0m,
            CreditCardStatus status = CreditCardStatus.Active,
            string expirationDate = "12/30")
        {
            var card = new CreditCard
            {
                Id = 0,
                CardNumber = cardNumber,
                ClientId = clientId,
                CreditLimit = creditLimit,
                CurrentDebt = currentDebt,
                ExpirationDate = expirationDate,
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

        private async Task<Beneficiary> SeedBeneficiaryAsync(string clientId, int savingsAccountId)
        {
            var beneficiary = new Beneficiary
            {
                Id = 0,
                ClientId = clientId,
                SavingsAccountId = savingsAccountId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Beneficiaries.Add(beneficiary);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(beneficiary).State = EntityState.Detached;
            return beneficiary;
        }

       
        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Return_Failure_When_Source_Account_Not_Owned_By_Client()
        {
            // Arrange
            await SeedAccountAsync("other-client", "400000001");
            await SeedAccountAsync("client-2", "400000002");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000001", DestinationAccountNumber = "400000002", Amount = 100m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected source account is not valid.");
        }

        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Return_Failure_When_Destination_Account_Does_Not_Exist()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000003");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000003", DestinationAccountNumber = "000000000", Amount = 100m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The account number entered does not correspond to a valid account.");
        }

        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Return_Failure_When_Same_Account()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000004");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000004", DestinationAccountNumber = "400000004", Amount = 100m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The destination account cannot be the same as the source account.");
        }

        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Return_Failure_When_Amount_Is_Zero_Or_Negative()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000005");
            await SeedAccountAsync("client-2", "400000006");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000005", DestinationAccountNumber = "400000006", Amount = 0m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount to transfer must be greater than zero.");
        }

        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Log_Rejected_Transaction_When_Insufficient_Funds()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000007", balance: 50m);
            await SeedAccountAsync("client-2", "400000008");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000007", DestinationAccountNumber = "400000008", Amount = 100m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount entered exceeds the available balance of the selected account.");
            var rejected = await _dbContext.Transactions.SingleAsync();
            rejected.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task ValidateExpressTransactionAsync_Should_Return_Success_With_Confirmation_Data()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000009", balance: 500m);
            await SeedAccountAsync("client-2", "400000010");
            var dto = new ExpressTransactionDto { SourceAccountNumber = "400000009", DestinationAccountNumber = "400000010", Amount = 100m };

            // Act
            var result = await _service.ValidateExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.DestinationAccountHolderName.Should().Be("Client client-2");
            result.Value.Amount.Should().Be(100m);
        }

        
        [Fact]
        public async Task ConfirmExpressTransactionAsync_Should_Return_Failure_When_Insufficient_Funds()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000011", balance: 10m);
            await SeedAccountAsync("client-2", "400000012");
            var dto = new ExpressTransactionConfirmationDto
            {
                SourceAccountNumber = "400000011",
                DestinationAccountNumber = "400000012",
                DestinationAccountHolderName = "Whoever",
                Amount = 100m
            };

            // Act
            var result = await _service.ConfirmExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The amount entered exceeds the available balance of the selected account.");
        }

        [Fact]
        public async Task ConfirmExpressTransactionAsync_Should_Move_Balances_And_Create_Transactions_When_Valid()
        {
            // Arrange
            var source = await SeedAccountAsync("client-1", "400000013", balance: 500m);
            var destination = await SeedAccountAsync("client-2", "400000014", balance: 100m);
            var dto = new ExpressTransactionConfirmationDto
            {
                SourceAccountNumber = "400000013",
                DestinationAccountNumber = "400000014",
                DestinationAccountHolderName = "Client client-2",
                Amount = 150m
            };

            // Act
            var result = await _service.ConfirmExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Be("The transaction was completed successfully.");

            var updatedSource = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == source.Id);
            var updatedDestination = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == destination.Id);
            updatedSource.Balance.Should().Be(350m);
            updatedDestination.Balance.Should().Be(250m);

            (await _dbContext.Transactions.CountAsync()).Should().Be(2);
        }

        [Fact]
        public async Task ConfirmExpressTransactionAsync_Should_Return_Success_With_Warning_Message_When_Email_Fails()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000015", balance: 500m);
            await SeedAccountAsync("client-2", "400000016");
            _emailServiceMock.Setup(s => s.SendAsync(It.IsAny<EmailRequestDto>())).ReturnsAsync(Result.Failure("SMTP down"));
            var dto = new ExpressTransactionConfirmationDto
            {
                SourceAccountNumber = "400000015",
                DestinationAccountNumber = "400000016",
                DestinationAccountHolderName = "Whoever",
                Amount = 50m
            };

            // Act
            var result = await _service.ConfirmExpressTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Be("The transaction was completed successfully, but one or more notification emails could not be sent.");
        }

        
        [Fact]
        public async Task PayCreditCardAsync_Should_Return_Failure_When_Card_Not_Owned_By_Client()
        {
            // Arrange
            var card = await SeedCreditCardAsync("other-client", "5000000000000001", currentDebt: 200m);
            await SeedAccountAsync("client-1", "400000017");
            var dto = new ClientCreditCardPaymentDto { SourceAccountNumber = "400000017", CreditCardId = card.Id, Amount = 100m };

            // Act
            var result = await _service.PayCreditCardAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected credit card is not valid.");
        }

        [Fact]
        public async Task PayCreditCardAsync_Should_Return_Failure_When_Card_Has_No_Debt()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000002", currentDebt: 0m);
            await SeedAccountAsync("client-1", "400000018");
            var dto = new ClientCreditCardPaymentDto { SourceAccountNumber = "400000018", CreditCardId = card.Id, Amount = 100m };

            // Act
            var result = await _service.PayCreditCardAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected card has no pending debt.");
        }

        [Fact]
        public async Task PayCreditCardAsync_Should_Log_Rejected_Payment_When_Insufficient_Funds()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000003", currentDebt: 300m);
            await SeedAccountAsync("client-1", "400000019", balance: 10m);
            var dto = new ClientCreditCardPaymentDto { SourceAccountNumber = "400000019", CreditCardId = card.Id, Amount = 100m };

            // Act
            var result = await _service.PayCreditCardAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You do not have the required amount in the selected account.");
            var rejected = await _dbContext.Transactions.SingleAsync();
            rejected.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task PayCreditCardAsync_Should_Cap_Payment_At_CurrentDebt_When_Amount_Exceeds_Debt()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000004", currentDebt: 80m);
            var account = await SeedAccountAsync("client-1", "400000020", balance: 500m);
            var dto = new ClientCreditCardPaymentDto { SourceAccountNumber = "400000020", CreditCardId = card.Id, Amount = 200m };

            // Act
            var result = await _service.PayCreditCardAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updatedCard = await _dbContext.CreditCards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
            var updatedAccount = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            updatedCard.CurrentDebt.Should().Be(0m);
            updatedAccount.Balance.Should().Be(420m); 
        }

        
        [Fact]
        public async Task ValidateBeneficiaryTransactionAsync_Should_Return_Failure_When_Beneficiary_Not_Owned_By_Client()
        {
            // Arrange
            var destinationAccount = await SeedAccountAsync("owner-1", "400000021");
            var beneficiary = await SeedBeneficiaryAsync("other-client", destinationAccount.Id);
            await SeedAccountAsync("client-1", "400000022");
            var dto = new BeneficiaryTransactionDto { SourceAccountNumber = "400000022", BeneficiaryId = beneficiary.Id, Amount = 100m };

            // Act
            var result = await _service.ValidateBeneficiaryTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected beneficiary is not valid.");
        }

        [Fact]
        public async Task ValidateBeneficiaryTransactionAsync_Should_Return_Failure_When_Beneficiary_Account_Is_Cancelled()
        {
            // Arrange
            var destinationAccount = await SeedAccountAsync("owner-1", "400000023", status: SavingsAccountStatus.Cancelled);
            var beneficiary = await SeedBeneficiaryAsync("client-1", destinationAccount.Id);
            await SeedAccountAsync("client-1", "400000024");
            var dto = new BeneficiaryTransactionDto { SourceAccountNumber = "400000024", BeneficiaryId = beneficiary.Id, Amount = 100m };

            // Act
            var result = await _service.ValidateBeneficiaryTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The beneficiary account is not available.");
        }

        [Fact]
        public async Task ValidateBeneficiaryTransactionAsync_Should_Return_Success_When_Valid()
        {
            // Arrange
            var destinationAccount = await SeedAccountAsync("owner-1", "400000025");
            var beneficiary = await SeedBeneficiaryAsync("client-1", destinationAccount.Id);
            await SeedAccountAsync("client-1", "400000026", balance: 500m);
            var dto = new BeneficiaryTransactionDto { SourceAccountNumber = "400000026", BeneficiaryId = beneficiary.Id, Amount = 200m };

            // Act
            var result = await _service.ValidateBeneficiaryTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.DestinationAccountNumber.Should().Be("400000025");
        }

        
        [Fact]
        public async Task ConfirmBeneficiaryTransactionAsync_Should_Move_Balances_When_Valid()
        {
            // Arrange
            var source = await SeedAccountAsync("client-1", "400000027", balance: 500m);
            var destination = await SeedAccountAsync("owner-1", "400000028", balance: 50m);
            var dto = new BeneficiaryTransactionConfirmationDto
            {
                SourceAccountNumber = "400000027",
                DestinationAccountNumber = "400000028",
                DestinationAccountHolderName = "Owner",
                Amount = 100m
            };

            // Act
            var result = await _service.ConfirmBeneficiaryTransactionAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updatedSource = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == source.Id);
            var updatedDestination = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == destination.Id);
            updatedSource.Balance.Should().Be(400m);
            updatedDestination.Balance.Should().Be(150m);
        }

       
        [Fact]
        public async Task ValidateOwnAccountTransferAsync_Should_Return_Failure_When_Less_Than_Two_Active_Accounts()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000029");
            var dto = new OwnAccountTransferDto { SourceAccountNumber = "400000029", DestinationAccountNumber = "400000030", Amount = 50m };

            // Act
            var result = await _service.ValidateOwnAccountTransferAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You must have at least two active savings accounts to make a transfer between accounts.");
        }

        [Fact]
        public async Task ValidateOwnAccountTransferAsync_Should_Return_Failure_When_Destination_Not_Owned_By_Client()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000031", type: SavingsAccountType.Principal);
            await SeedAccountAsync("client-1", "400000032", type: SavingsAccountType.Secondary);
            await SeedAccountAsync("other-client", "400000033");
            var dto = new OwnAccountTransferDto { SourceAccountNumber = "400000031", DestinationAccountNumber = "400000033", Amount = 50m };

            // Act
            var result = await _service.ValidateOwnAccountTransferAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected destination account is not valid.");
            var rejected = await _dbContext.Transactions.SingleAsync();
            rejected.Status.Should().Be(TransactionStatus.Rejected);
        }

        [Fact]
        public async Task ValidateOwnAccountTransferAsync_Should_Return_Success_When_Valid()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000034", balance: 300m, type: SavingsAccountType.Principal);
            await SeedAccountAsync("client-1", "400000035", type: SavingsAccountType.Secondary);
            var dto = new OwnAccountTransferDto { SourceAccountNumber = "400000034", DestinationAccountNumber = "400000035", Amount = 100m };

            // Act
            var result = await _service.ValidateOwnAccountTransferAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.Amount.Should().Be(100m);
        }

        
        [Fact]
        public async Task ConfirmOwnAccountTransferAsync_Should_Move_Balances_When_Valid()
        {
            // Arrange
            var source = await SeedAccountAsync("client-1", "400000036", balance: 300m, type: SavingsAccountType.Principal);
            var destination = await SeedAccountAsync("client-1", "400000037", balance: 20m, type: SavingsAccountType.Secondary);
            var dto = new OwnAccountTransferConfirmationDto { SourceAccountNumber = "400000036", DestinationAccountNumber = "400000037", Amount = 100m };

            // Act
            var result = await _service.ConfirmOwnAccountTransferAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var updatedSource = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == source.Id);
            var updatedDestination = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == destination.Id);
            updatedSource.Balance.Should().Be(200m);
            updatedDestination.Balance.Should().Be(120m);
        }

        [Fact]
        public async Task ConfirmOwnAccountTransferAsync_Should_Return_Failure_When_Insufficient_Funds()
        {
            // Arrange
            await SeedAccountAsync("client-1", "400000038", balance: 10m, type: SavingsAccountType.Principal);
            await SeedAccountAsync("client-1", "400000039", type: SavingsAccountType.Secondary);
            var dto = new OwnAccountTransferConfirmationDto { SourceAccountNumber = "400000038", DestinationAccountNumber = "400000039", Amount = 100m };

            // Act
            var result = await _service.ConfirmOwnAccountTransferAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("You do not have the required amount in the selected account.");
        }

      
        [Fact]
        public async Task RequestCashAdvanceAsync_Should_Return_Failure_When_Card_Not_Active()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000005", status: CreditCardStatus.Cancelled);
            await SeedAccountAsync("client-1", "400000040");
            var dto = new CashAdvanceDto { CreditCardId = card.Id, DestinationAccountNumber = "400000040", Amount = 100m };

            // Act
            var result = await _service.RequestCashAdvanceAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected card is not active.");
        }

        [Fact]
        public async Task RequestCashAdvanceAsync_Should_Return_Failure_When_Card_Is_Expired()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000006", expirationDate: "01/20");
            await SeedAccountAsync("client-1", "400000041");
            var dto = new CashAdvanceDto { CreditCardId = card.Id, DestinationAccountNumber = "400000041", Amount = 100m };

            // Act
            var result = await _service.RequestCashAdvanceAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The selected card is expired.");
        }

        [Fact]
        public async Task RequestCashAdvanceAsync_Should_Reject_And_Log_Consumption_When_Exceeds_Available_Credit()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000007", creditLimit: 100m, currentDebt: 0m);
            await SeedAccountAsync("client-1", "400000042");
            var dto = new CashAdvanceDto { CreditCardId = card.Id, DestinationAccountNumber = "400000042", Amount = 100m };

            // Act
            var result = await _service.RequestCashAdvanceAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("The requested advance exceeds the available credit of the selected card.");
            var consumption = await _dbContext.CardConsumptions.SingleAsync();
            consumption.Status.Should().Be(ConsumptionStatus.Rejected);
        }

        [Fact]
        public async Task RequestCashAdvanceAsync_Should_Credit_Account_And_Charge_Card_With_Interest_When_Valid()
        {
            // Arrange
            var card = await SeedCreditCardAsync("client-1", "5000000000000008", creditLimit: 5000m, currentDebt: 0m);
            var account = await SeedAccountAsync("client-1", "400000043", balance: 100m);
            var dto = new CashAdvanceDto { CreditCardId = card.Id, DestinationAccountNumber = "400000043", Amount = 200m };

            // Act
            var result = await _service.RequestCashAdvanceAsync(dto, "client-1");

            // Assert
            result.IsSuccess.Should().BeTrue();

            var expectedInterest = Math.Round(200m * 0.0625m, 2, MidpointRounding.AwayFromZero); // 12.50
            var expectedTotal = 200m + expectedInterest; // 212.50

            var updatedAccount = await _dbContext.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
            var updatedCard = await _dbContext.CreditCards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
            updatedAccount.Balance.Should().Be(300m); 
            updatedCard.CurrentDebt.Should().Be(expectedTotal);

            var approvedConsumption = await _dbContext.CardConsumptions.SingleAsync();
            approvedConsumption.Status.Should().Be(ConsumptionStatus.Approved);
            approvedConsumption.Amount.Should().Be(expectedTotal);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
