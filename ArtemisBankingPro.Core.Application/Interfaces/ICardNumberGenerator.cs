namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ICardNumberGenerator
    {
        Task<string> GenerateAsync();
    }
}
