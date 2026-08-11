using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class SavingsAccountDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<SavingsAccountDto,SavingsAccountViewModel>();
        }
    }
}
