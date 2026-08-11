using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class TransactionDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<TransactionDto, TransactionViewModel>();
        }
    }
}
