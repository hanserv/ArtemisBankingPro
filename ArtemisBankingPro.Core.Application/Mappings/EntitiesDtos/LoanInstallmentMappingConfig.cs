using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class LoanInstallmentMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<LoanInstallment, LoanInstallmentDto>()
                .Map(dest => dest.CapitalAmount, src => src.PrincipalAmount)
                .Map(dest => dest.PendingInstallmentAmount, src => src.RemainingBalance)
                .Map(dest => dest.PaymentStatus, src => src.Status);
        }
    }
}
