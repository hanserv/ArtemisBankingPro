using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ICreditCardService
    {
        Task<Result> AssignCreditCardAsync(AssignCreditCardDto dto, string createdByAdminId);
        Task<Result> CancelCreditCardAsync(int creditCardId, string performedByAdminId);
        Task<Result<CreditCardDto>> GetByIdAsync(int id);
        Task<Result<List<CardConsumptionDto>>> GetConsumptionsAsync(int creditCardId);
        Task<Result<PagedResult<CreditCardDto>>> GetPagedAsync(CreditCardFilterDto filter);
        Task<Result> ModifyCreditCardLimitAsync(ModifyCreditCardLimitDto dto, string performedByAdminId);
        Task<Result> ValidateClientForAssignmentAsync(string? clientId);
    }
}
