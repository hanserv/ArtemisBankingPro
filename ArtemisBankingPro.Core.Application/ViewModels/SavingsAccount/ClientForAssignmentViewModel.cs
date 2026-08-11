namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class ClientForAssignmentViewModel : BaseViewModel<string>
    {
        public required string Identification { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public decimal TotalDebt { get; set; }
    }
}
