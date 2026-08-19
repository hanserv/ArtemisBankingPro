using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class CardConsumptionRepository : GenericRepository<CardConsumption>, ICardConsumptionRepository
    {
        public CardConsumptionRepository(ArtemisBankingProContext context) : base(context)
        {
        }
    }
}
