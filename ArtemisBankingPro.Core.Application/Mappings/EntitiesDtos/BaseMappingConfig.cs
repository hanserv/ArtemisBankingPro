using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Domain.Common;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.EntitiesDtos
{
    public class BaseMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig(typeof(BaseEntity<>), typeof(BaseDto<>));
            config.NewConfig(typeof(BaseDto<>), typeof(BaseEntity<>));
        }
    }
}
