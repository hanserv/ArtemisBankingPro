namespace ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount
{
    public class AssignSelectClientViewModel
    {
        public string? Identification { get; set; }
        public string? SelectedClientId { get; set; }
        public List<ClientForAssignmentViewModel> Clients { get; set; } = [];
    }
}
