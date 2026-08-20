using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class UserFilterViewModel
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public UserType? Role { get; set; }
    }
}
