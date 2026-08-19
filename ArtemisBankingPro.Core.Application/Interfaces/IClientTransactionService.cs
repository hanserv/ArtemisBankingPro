using ArtemisBankingPro.Core.Application.DTOs.Transaction;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface IClientTransactionService
    {
        Task<Result<ExpressTransactionConfirmationDto>> ValidateExpressTransactionAsync(ExpressTransactionDto dto, string clientId);
        Task<Result> ConfirmExpressTransactionAsync(ExpressTransactionConfirmationDto dto, string clientId);
        Task<Result> PayCreditCardAsync(ClientCreditCardPaymentDto dto, string clientId);
        Task<Result<BeneficiaryTransactionConfirmationDto>> ValidateBeneficiaryTransactionAsync(BeneficiaryTransactionDto dto, string clientId);
        Task<Result> ConfirmBeneficiaryTransactionAsync(BeneficiaryTransactionConfirmationDto dto, string clientId);
        Task<Result<OwnAccountTransferConfirmationDto>> ValidateOwnAccountTransferAsync(OwnAccountTransferDto dto, string clientId);
        Task<Result> ConfirmOwnAccountTransferAsync(OwnAccountTransferConfirmationDto dto, string clientId);
        Task<Result> RequestCashAdvanceAsync(CashAdvanceDto dto, string clientId);
    }
}
