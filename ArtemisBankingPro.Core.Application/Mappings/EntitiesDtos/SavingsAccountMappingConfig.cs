using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class SavingsAccountMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<SavingsAccount, SavingsAccountDto>()
                .Ignore(dest => dest.ClientFullName);
        }
    }
}
