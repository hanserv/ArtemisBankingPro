using System.Security.Claims;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.Dashboard;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly IMapper _mapper;

        public HomeController(IDashboardService dashboardService, IMapper mapper)
        {
            _dashboardService = dashboardService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _dashboardService.GetClientProductsAsync(currentUserId);

            var vm = result.IsSuccess
                ? new ClientHomeViewModel
                {
                    SavingsAccounts = _mapper.Map<List<SavingsAccountViewModel>>(result.Value!.SavingsAccounts),
                    Loans = _mapper.Map<List<LoanViewModel>>(result.Value.Loans),
                    CreditCards = _mapper.Map<List<CreditCardViewModel>>(result.Value.CreditCards)
                }
                : new ClientHomeViewModel { SavingsAccounts = [], Loans = [], CreditCards = [] };

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
            }

            return View(vm);
        }
    }
}
