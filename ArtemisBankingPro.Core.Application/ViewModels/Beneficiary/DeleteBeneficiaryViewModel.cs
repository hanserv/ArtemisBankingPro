namespace ArtemisBankingPro.Core.Application.ViewModels.Beneficiary
{
    public class DeleteBeneficiaryViewModel : BaseViewModel<int>
    {
        public required string FullName { get; set; }
        public required string AccountNumber { get; set; }
    }
}
