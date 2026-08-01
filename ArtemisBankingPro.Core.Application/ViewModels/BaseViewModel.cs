namespace ArtemisBankingPro.Core.Application.ViewModels
{
    public class BaseViewModel<TKey>
    {
        public required TKey Id { get; set; }
    }
}
