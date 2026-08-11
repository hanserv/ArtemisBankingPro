using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class TransactionMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Transaction, TransactionDto>()
            .Map(dest => dest.TransactionType, src => src.Type)
            .Map(dest => dest.Date, src => src.CreatedAt);
        }
    }
}
