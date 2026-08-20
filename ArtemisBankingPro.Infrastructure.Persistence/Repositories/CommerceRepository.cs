using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Persistence.Contexts;

namespace ArtemisBankingPro.Infrastructure.Persistence.Repositories
{
    public class CommerceRepository : GenericRepository<Commerce>, ICommerceRepository
    {
        public CommerceRepository(ArtemisBankingProContext context) : base(context)
        {
        }
    }
}
