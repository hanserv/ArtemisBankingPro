using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Dashboard;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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
            var result = await _dashboardService.GetAdminSummaryAsync();

            var vm = result.IsSuccess ? _mapper.Map<AdminDashboardViewModel>(result.Value!) : null;

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
            }

            return View(vm);
        }
    }
}
