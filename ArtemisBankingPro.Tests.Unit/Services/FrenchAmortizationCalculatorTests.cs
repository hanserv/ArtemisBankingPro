using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using FluentAssertions;

namespace ArtemisBankingPro.Tests.Unit.Services
{
    public class FrenchAmortizationCalculatorTests
    {
        private LoanInstallment CreateDummyInstallment(int id, int number)
        {
            return new LoanInstallment
            {
                Id = id,
                InstallmentNumber = number,
                DueDate = DateTime.UtcNow.AddMonths(number),
                InstallmentAmount = 0m,
                InterestAmount = 0m,
                PrincipalAmount = 0m,
                RemainingBalance = 0m,
                Status = InstallmentStatus.Pending,
                IsLate = false,
                LoanId = 1
            };
        }

        
        [Fact]
        public void GetMonthlyRate_Should_Calculate_Correct_Monthly_Decimal()
        {
            // Arrange
            var annualRate = 12m;

            // Act
            var result = FrenchAmortizationCalculator.GetMonthlyRate(annualRate);

            // Assert
            result.Should().Be(0.01m);
        }

        [Fact]
        public void GetMonthlyRate_Should_Return_Zero_When_Annual_Rate_Is_Zero()
        {
            // Arrange
            var annualRate = 0m;

            // Act
            var result = FrenchAmortizationCalculator.GetMonthlyRate(annualRate);

            // Assert
            result.Should().Be(0m);
        }

       
        [Fact]
        public void CalculateMonthlyInstallment_Should_Divide_By_Term_When_Interest_Is_Zero()
        {
            // Arrange
            var principal = 12000m;
            var annualRate = 0m;
            var term = 12;

            // Act
            var result = FrenchAmortizationCalculator.CalculateMonthlyInstallment(principal, annualRate, term);

            // Assert
            result.Should().Be(1000m);
        }

        [Fact]
        public void CalculateMonthlyInstallment_Should_Calculate_Correct_French_Installment()
        {
            // Arrange
            var principal = 10000m;
            var rate = 12m;
            var term = 12;

            // Act
            var result = FrenchAmortizationCalculator.CalculateMonthlyInstallment(principal, rate, term);

            // Assert
            result.Should().Be(888.49m);
        }

       
        [Fact]
        public void GenerateSchedule_Should_Create_Correct_Number_Of_Installments_And_Dates()
        {
            // Arrange
            var principal = 10000m;
            var rate = 12m;
            var term = 12;
            var startDate = new DateTime(2026, 1, 1);

            // Act
            var result = FrenchAmortizationCalculator.GenerateSchedule(principal, rate, term, startDate);

            // Assert
            result.Should().HaveCount(12);
            result.First().DueDate.Should().Be(new DateTime(2026, 2, 1));
            result.Last().DueDate.Should().Be(new DateTime(2027, 1, 1));
            result.Should().AllSatisfy(i => i.Status.Should().Be(InstallmentStatus.Pending));
        }

        [Fact]
        public void GenerateSchedule_Should_Amortize_Total_Principal_Exactly()
        {
            // Arrange
            var principal = 15000m;
            var rate = 14.5m;
            var term = 36;
            var startDate = DateTime.UtcNow;

            // Act
            var result = FrenchAmortizationCalculator.GenerateSchedule(principal, rate, term, startDate);

            // Assert
            var totalPrincipalAmortized = result.Sum(i => i.PrincipalAmount);
            totalPrincipalAmortized.Should().Be(principal);
        }

        [Fact]
        public void GenerateSchedule_Should_Adjust_Last_Installment_Correctly()
        {
            // Arrange
            var principal = 10000m;
            var rate = 12m;
            var term = 12;
            var startDate = DateTime.UtcNow;

            // Act
            var result = FrenchAmortizationCalculator.GenerateSchedule(principal, rate, term, startDate);

            // Assert
            var lastInstallment = result.Last();
            var expectedAmount = lastInstallment.PrincipalAmount + lastInstallment.InterestAmount;

            lastInstallment.InstallmentAmount.Should().Be(expectedAmount);
        }

        
        [Fact]
        public void RecalculateInstallments_Should_Update_Amounts_And_Sum_To_New_Outstanding_Principal()
        {
            // Arrange
            var pendingInstallments = new List<LoanInstallment>
            {
                CreateDummyInstallment(1, 1),
                CreateDummyInstallment(2, 2),
                CreateDummyInstallment(3, 3),
                CreateDummyInstallment(4, 4),
                CreateDummyInstallment(5, 5),
                CreateDummyInstallment(6, 6)
            };

            var newOutstandingPrincipal = 4500m;
            var rate = 12m;

            // Act
            FrenchAmortizationCalculator.RecalculateInstallments(pendingInstallments, newOutstandingPrincipal, rate);

            // Assert
            var totalPrincipalRecalculated = pendingInstallments.Sum(i => i.PrincipalAmount);
            totalPrincipalRecalculated.Should().Be(newOutstandingPrincipal);

            pendingInstallments.Should().AllSatisfy(i =>
            {
                i.InstallmentAmount.Should().BeGreaterThan(0);
                i.PrincipalAmount.Should().BeGreaterThan(0);
                i.RemainingBalance.Should().Be(i.InstallmentAmount);
            });
        }

        [Fact]
        public void RecalculateInstallments_Should_Adjust_Last_Installment_Correctly()
        {
            // Arrange
            var pendingInstallments = new List<LoanInstallment>
            {
                CreateDummyInstallment(1, 1),
                CreateDummyInstallment(2, 2),
                CreateDummyInstallment(3, 3)
            };
            var newOutstandingPrincipal = 2000m;
            var rate = 15m;

            // Act
            FrenchAmortizationCalculator.RecalculateInstallments(pendingInstallments, newOutstandingPrincipal, rate);

            // Assert
            var lastInstallment = pendingInstallments.Last();
            var expectedAmount = lastInstallment.PrincipalAmount + lastInstallment.InterestAmount;

            lastInstallment.InstallmentAmount.Should().Be(expectedAmount);
        }
    }
}
