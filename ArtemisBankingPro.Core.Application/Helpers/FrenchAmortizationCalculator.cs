using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Application.Helpers
{
    public static class FrenchAmortizationCalculator
    {
        public static decimal GetMonthlyRate(decimal annualInterestRate) => (annualInterestRate / 100m) / 12m;

        public static decimal CalculateMonthlyInstallment(decimal principal, decimal annualInterestRate, int termInMonths)
        {
            var monthlyRate = GetMonthlyRate(annualInterestRate);

            if (monthlyRate == 0)
            {
                return Math.Round(principal / termInMonths, 2, MidpointRounding.AwayFromZero);
            }

            var compoundFactor = Math.Pow((double)(1 + monthlyRate), termInMonths);
            var installment = principal * (decimal)((double)monthlyRate * compoundFactor) / (decimal)(compoundFactor - 1);

            return Math.Round(installment, 2, MidpointRounding.AwayFromZero);
        }

        public static List<LoanInstallment> GenerateSchedule(decimal principal, decimal annualInterestRate, int termInMonths, DateTime loanCreatedAt)
        {
            var monthlyRate = GetMonthlyRate(annualInterestRate);
            var installmentAmount = CalculateMonthlyInstallment(principal, annualInterestRate, termInMonths);
            var outstandingCapital = principal;

            var schedule = new List<LoanInstallment>(termInMonths);

            for (var i = 1; i <= termInMonths; i++)
            {
                var interestAmount = Math.Round(outstandingCapital * monthlyRate, 2, MidpointRounding.AwayFromZero);
                var principalAmount = installmentAmount - interestAmount;
                var currentInstallmentAmount = installmentAmount;

                if (i == termInMonths)
                {
                    principalAmount = outstandingCapital;
                    currentInstallmentAmount = principalAmount + interestAmount;
                }

                outstandingCapital -= principalAmount;

                schedule.Add(new LoanInstallment
                {
                    Id = 0,
                    InstallmentNumber = i,
                    DueDate = loanCreatedAt.AddMonths(i),
                    InstallmentAmount = currentInstallmentAmount,
                    InterestAmount = interestAmount,
                    PrincipalAmount = principalAmount,
                    RemainingBalance = currentInstallmentAmount,
                    Status = InstallmentStatus.Pending,
                    IsLate = false,
                    LoanId = 0
                });
            }

            return schedule;
        }

        public static void RecalculateInstallments(List<LoanInstallment> installmentsToRecalculate, decimal outstandingPrincipal, decimal annualInterestRate)
        {
            var count = installmentsToRecalculate.Count;
            var monthlyRate = GetMonthlyRate(annualInterestRate);
            var installmentAmount = CalculateMonthlyInstallment(outstandingPrincipal, annualInterestRate, count);
            var outstandingCapital = outstandingPrincipal;

            for (var i = 0; i < count; i++)
            {
                var installment = installmentsToRecalculate[i];
                var interestAmount = Math.Round(outstandingCapital * monthlyRate, 2, MidpointRounding.AwayFromZero);
                var principalAmount = installmentAmount - interestAmount;
                var currentInstallmentAmount = installmentAmount;

                var isLast = i == count - 1;
                if (isLast)
                {
                    principalAmount = outstandingCapital;
                    currentInstallmentAmount = principalAmount + interestAmount;
                }

                outstandingCapital -= principalAmount;

                installment.InstallmentAmount = currentInstallmentAmount;
                installment.InterestAmount = interestAmount;
                installment.PrincipalAmount = principalAmount;
                installment.RemainingBalance = currentInstallmentAmount;
            }
        }
    }
}
