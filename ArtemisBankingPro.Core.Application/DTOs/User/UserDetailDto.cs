using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class UserDetailDto : UserDto
    {
        public required SavingsAccountDto MainAccount { get; set; }
    }
}
