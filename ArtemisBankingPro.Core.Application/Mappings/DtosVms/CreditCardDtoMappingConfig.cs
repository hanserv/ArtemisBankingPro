using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class CreditCardDtoMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<CreditCardDto, CreditCardViewModel>();
            config.NewConfig<CardConsumptionDto, CardConsumptionViewModel>();
            config.NewConfig<CreditCardDto, CancelCreditCardViewModel>();
        }
    }
}
