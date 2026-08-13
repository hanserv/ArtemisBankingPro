using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.DTOs.User;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ILoanService
    {
        Task<Result<AssignLoanResultDto>> AssignAsync(AssignLoanDto dto);
        Task<Result<LoanDto>> GetByIdAsync(int id);
        Task<Result<List<ClientForAssignmentDto>>> GetClientsEligibleForLoanAsync(string? identification);
        Task<Result<LoanDetailsDto>> GetDetailsAsync(int id);
        Task<Result<PagedResult<LoanDto>>> GetPagedAsync(LoanFilterDto filter);
        Task<int> MarkOverdueInstallmentsAsync();
        Task<Result> ModifyRateAsync(ModifyLoanRateDto dto, string performedByAdminId);
        Task<Result> ValidateClientForAssignmentAsync(string? clientId);
    }
}
