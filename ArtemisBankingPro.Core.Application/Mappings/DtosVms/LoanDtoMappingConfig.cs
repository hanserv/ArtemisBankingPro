using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class LoanDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<LoanDto, LoanViewModel>();
            config.NewConfig<LoanFilterViewModel, LoanFilterDto>();
            config.NewConfig<ModifyLoanRateViewModel, ModifyLoanRateDto>();
        }
    }
}
