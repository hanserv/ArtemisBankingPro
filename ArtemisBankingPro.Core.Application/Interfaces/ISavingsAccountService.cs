using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ISavingsAccountService
    {
        Task<Result<SavingsAccountDto>> GetByIdAsync(int id);
        Task<Result> CreatePrincipalAccountAsync(string clientId, decimal initialAmount);
        Task<Result> CreateSecondaryAccountAsync(string clientId, decimal initialBalance, string createdByAdminId);
        Task<Result> CreditAdditionalAmountAsync(string clientId, decimal amount, string performedByUserId);
        Task<Result<List<ClientForAssignmentDto>>> GetClientsForAssignmentAsync(string? identification);
        Task<Result<PagedResult<SavingsAccountDto>>> GetPagedAsync(SavingsAccountFilterDto filter);
        Task<Result> ValidateClientForAssignmentAsync(string? clientId);
        Task<Result> CancelSecondaryAccountAsync(int accountId, string performedByAdminId);
    }
}
