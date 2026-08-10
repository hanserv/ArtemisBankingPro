namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IAccountNumberGenerator
    {
        Task<string> GenerateAsync();
    }
}
