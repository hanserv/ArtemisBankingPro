using ArtemisBankingPro.Core.Application.DTOs;

namespace ArtemisBankingPro.Core.Application.ViewModels.User
{
    public class UserListViewModel
    {
        public required UserFilterViewModel Filter { get; set; }
        public required PagedResult<UserViewModel> Users { get; set; }
    }
}
