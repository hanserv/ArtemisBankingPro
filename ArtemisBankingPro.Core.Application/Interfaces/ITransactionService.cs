using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<Result<PagedResult<TransactionDto>>> GetAccountTransactionsAsync(int accountId, int page, int pageSize);
        Task<Result<DepositConfirmationDto>> ValidateDepositAsync(DepositDto dto);
        Task<Result> ConfirmDepositAsync(DepositConfirmationDto dto, string cashierId);
        Task<Result<WithdrawalConfirmationDto>> ValidateWithdrawalAsync(WithdrawalDto dto, string cashierId);
        Task<Result> ConfirmWithdrawalAsync(WithdrawalConfirmationDto dto, string cashierId);
        Task<Result<CreditCardPaymentConfirmationDto>> ValidateCreditCardPaymentAsync(CreditCardPaymentDto dto, string cashierId);
        Task<Result> ConfirmCreditCardPaymentAsync(CreditCardPaymentConfirmationDto dto, string cashierId);
        Task<Result<ThirdPartyTransactionConfirmationDto>> ValidateThirdPartyTransactionAsync(ThirdPartyTransactionDto dto, string cashierId);
        Task<Result> ConfirmThirdPartyTransactionAsync(ThirdPartyTransactionConfirmationDto dto, string cashierId);
    }
}
