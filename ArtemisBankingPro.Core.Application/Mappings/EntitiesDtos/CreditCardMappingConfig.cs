using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Domain.Entities;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class CreditCardMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<CreditCard, CreditCardDto>()
                .Map(dest => dest.LastFourDigits, src => src.CardNumber.Substring(src.CardNumber.Length - 4))
                .Ignore(dest => dest.ClientFullName)
                .Ignore(dest => dest.CreatedByAdminName);
        }
    }
}
