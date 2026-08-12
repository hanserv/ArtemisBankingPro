using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class CardConsumptionMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<CardConsumption, CardConsumptionDto>()
                .Map(dest => dest.Date, src => src.ConsumptionDate)
                .Map(dest => dest.CommerceName, src => src.Commerce != null ? src.Commerce.Name : "AVANCE");
        }
    }
}
