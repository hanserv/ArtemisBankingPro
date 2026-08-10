namespace ArtemisBankingPro.Core.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        Task ExecuteInTransactionAsync(Func<Task> operation);
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);
    }
}
