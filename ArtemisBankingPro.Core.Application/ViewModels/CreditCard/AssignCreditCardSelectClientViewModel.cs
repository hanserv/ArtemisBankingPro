using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;

namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class AssignCreditCardSelectClientViewModel
    {
        public string? Identification { get; set; }
        public string? SelectedClientId { get; set; }
        public decimal SystemAverageDebt { get; set; }
        public List<ClientForAssignmentViewModel> Clients { get; set; } = [];
    }
}
