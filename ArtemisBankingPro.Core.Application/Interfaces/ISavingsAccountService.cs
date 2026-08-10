namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ISavingsAccountService
    {
        Task<Result> CreatePrincipalAccountAsync(string clientId, decimal initialAmount);
        Task<Result> CreditAdditionalAmountAsync(string clientId, decimal amount, string performedByUserId);
    }
}
