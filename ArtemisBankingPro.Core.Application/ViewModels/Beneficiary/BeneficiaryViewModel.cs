namespace ArtemisBankingPro.Core.Application.ViewModels.Beneficiary
{
    public class BeneficiaryViewModel : BaseViewModel<int>
    {
        public required string FullName { get; set; }
        public required string AccountNumber { get; set; }
    }
}
