using ArtemisBankingPro.Core.Domain.Entities;

namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface ICreditCardRepository : IGenericRepository<CreditCard>
    {
        Task<bool> CardNumberExistsAsync(string cardNumber);
        Task<int> CountActiveAsync();
        Task<CreditCard?> GetByCardNumberAsync(string cardNumber);
    }
}
