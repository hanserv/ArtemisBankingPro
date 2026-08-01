namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface IGenericRepository<Entity> where Entity : class
    {
        Task<IEnumerable<Entity>> GetAllAsync();
        IQueryable<Entity> GetAllQuery();
        Task<IEnumerable<Entity>> GetAllListInclude(List<string> properties);
        IQueryable<Entity> GetAllQueryInclude(List<string> properties);
        Task<Entity?> GetByIdAsync(int id);
        Task<Entity> AddAsync(Entity entity);
        Task DeleteAsync(Entity entity);
        Task UpdateAsync(Entity entity);
    }
}
