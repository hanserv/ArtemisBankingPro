using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class LoanMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Loan, LoanDto>()
                .Map(dest => dest.MonthlyInstallment, src => src.Installments.Select(i => i.InstallmentAmount).FirstOrDefault())
                .Map(dest => dest.TotalInstallments, src => src.Installments.Count)
                .Map(dest => dest.PaidInstallments, src => src.Installments.Count(i => i.Status == InstallmentStatus.Paid))
                .Map(dest => dest.ClientPaymentStatus, src => src.Installments.Any(i => i.IsLate)
                    ? ClientPaymentStatus.InArrears
                    : ClientPaymentStatus.UpToDate)
                .Map(dest => dest.ClientFullName, src => string.Empty);

            config.NewConfig<AssignLoanDto, Loan>()
                .Map(dest => dest.CreatedByAdminId, src => src.AdminId)
                .Ignore(dest => dest.LoanNumber)
                .Ignore(dest => dest.PendingAmount)
                .Ignore(dest => dest.Status)
                .Ignore(dest => dest.CreatedAt)
                .Ignore(dest => dest.Installments);
        }
    }
}
