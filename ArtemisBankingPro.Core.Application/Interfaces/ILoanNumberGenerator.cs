namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ILoanNumberGenerator
    {
        Task<string> GenerateAsync();
    }
}
