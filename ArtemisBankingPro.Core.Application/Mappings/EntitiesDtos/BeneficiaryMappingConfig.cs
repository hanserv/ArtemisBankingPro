using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class BeneficiaryMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Beneficiary, BeneficiaryDto>()
                .Map(dest => dest.AccountNumber, src => src.SavingsAccount!.AccountNumber)
                .Ignore(dest => dest.FullName);

            config.NewConfig<AddBeneficiaryDto, Beneficiary>()
                .Ignore(dest => dest.Id)
                .Ignore(dest => dest.SavingsAccountId)
                .Ignore(dest => dest.SavingsAccount)
                .Ignore(dest => dest.CreatedAt);
        }
    }
}
