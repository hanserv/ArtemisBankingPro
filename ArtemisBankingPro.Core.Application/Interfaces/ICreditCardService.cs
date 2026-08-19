using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ICreditCardService
    {
        Task<Result> AssignCreditCardAsync(AssignCreditCardDto dto, string createdByAdminId);
        Task<Result> CancelCreditCardAsync(int creditCardId, string performedByAdminId);
        Task<Result<List<CreditCardDto>>> GetActiveCardsByClientIdAsync(string clientId);
        Task<Result<CreditCardDto>> GetByIdAsync(int id);
        Task<Result<CreditCardDto>> GetClientCardByIdAsync(int id, string clientId);
        Task<Result<List<CardConsumptionDto>>> GetClientCardConsumptionsAsync(int id, string clientId);
        Task<Result<List<CardConsumptionDto>>> GetConsumptionsAsync(int creditCardId);
        Task<Result<PagedResult<CreditCardDto>>> GetPagedAsync(CreditCardFilterDto filter);
        Task<Result> ModifyCreditCardLimitAsync(ModifyCreditCardLimitDto dto, string performedByAdminId);
        Task<Result> ValidateClientForAssignmentAsync(string? clientId);
    }
}
