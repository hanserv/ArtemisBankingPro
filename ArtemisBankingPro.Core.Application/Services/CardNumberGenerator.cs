using ArtemisBankingPro.Core.Application.Helpers;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Interfaces;

namespace ArtemisBankingPro.Core.Application.Services
{
    public class CardNumberGenerator : ICardNumberGenerator
    {
        private const int MaxAttempts = 25;

        private readonly ICreditCardRepository _creditCardRepository;

        public CardNumberGenerator(ICreditCardRepository creditCardRepository)
        {
            _creditCardRepository = creditCardRepository;
        }

        public async Task<string> GenerateAsync()
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var candidate = NumericStringGenerator.Generate(16);

                if (!await _creditCardRepository.CardNumberExistsAsync(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Could not generate a unique 16-digit card number after several attempts.");
        }
    }
}
