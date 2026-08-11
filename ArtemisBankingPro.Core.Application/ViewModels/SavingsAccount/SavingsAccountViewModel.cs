using ArtemisBankingPro.Core.Domain.Common.Enums;

namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class SavingsAccountViewModel : BaseViewModel<int>
    {
        public required string AccountNumber { get; set; }
        public required string ClientFullName { get; set; }
        public required decimal Balance { get; set; }
        public required SavingsAccountType Type { get; set; }
        public required SavingsAccountStatus Status { get; set; }
    }
}
