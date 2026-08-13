using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.Loan
{
    public class AssignLoanSelectClientViewModel
    {
        public string? Identification { get; set; }
        public decimal SystemAverageDebt { get; set; }
        public string? SelectedClientId { get; set; }
        public List<ClientForAssignmentViewModel> Clients { get; set; } = [];
    }
}
