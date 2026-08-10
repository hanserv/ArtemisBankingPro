using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class UserViewModel : BaseViewModel<string>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Identification { get; set; }
        public required string Role { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required bool IsActive { get; set; }
    }
}
