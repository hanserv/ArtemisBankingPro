using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;

namespace ArtemisBankingPro.Core.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<Result<PagedResult<TransactionDto>>> GetAccountTransactionsAsync(int accountId, int page, int pageSize);
    }
}
