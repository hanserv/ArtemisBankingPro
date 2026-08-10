using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class UserFilterViewModel
    {
        public UserType? Role { get; set; }
        public int Page { get; set; } = 1;
    }
}
