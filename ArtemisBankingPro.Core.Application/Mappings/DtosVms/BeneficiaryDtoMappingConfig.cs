using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;
using ArtemisBankingPro.Core.Application.ViewModels.Beneficiary;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class BeneficiaryDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<BeneficiaryDto, BeneficiaryViewModel>();
            config.NewConfig<AddBeneficiaryViewModel, AddBeneficiaryDto>();
            config.NewConfig<BeneficiaryDto, DeleteBeneficiaryViewModel>();
        }
    }
}
