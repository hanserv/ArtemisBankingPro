namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class ChangeUserStatusViewModel : BaseViewModel<string>
    {
        public required string FullName { get; set; }
        public bool IsActive { get; set; }
    }
}
