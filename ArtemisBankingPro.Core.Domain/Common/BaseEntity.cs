namespace ArtemisBankingPro.Core.Domain.Common
{
    public class BaseEntity<TKey>
    {
        public required TKey Id { get; set; }
    }
}
