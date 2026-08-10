namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class UpdateUserViewModel : BaseViewModel<string>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Identification { get; set; }
        public required string Email { get; set; }
        public required string UserName { get; set; }
        public required string Role { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public decimal? AdditionalAmount { get; set; }
    }
}
