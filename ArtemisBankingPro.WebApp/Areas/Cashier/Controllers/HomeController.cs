using System.Security.Claims;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Dashboard;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "Cashier")]
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

            var result = await _dashboardService.GetCashierSummaryAsync(currentUserId);

            var vm = result.IsSuccess ? _mapper.Map<CashierDashboardViewModel>(result.Value!) : null;

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
            }

            return View(vm);
        }
    }
}
