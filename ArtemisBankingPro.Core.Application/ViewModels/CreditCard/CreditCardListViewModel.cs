using ArtemisBankingPro.Core.Application.DTOs;

namespace ArtemisBankingPro.Core.Application.ViewModels.CreditCard
{
    public class CreditCardListViewModel
    {
        public required CreditCardFilterViewModel Filter { get; set; }
        public required PagedResult<CreditCardViewModel> Cards { get; set; }
    }
}
