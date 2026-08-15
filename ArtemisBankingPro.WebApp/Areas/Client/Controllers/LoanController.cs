using System.Security.Claims;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class LoanController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly IMapper _mapper;

        public LoanController(ILoanService loanService, IMapper mapper)
        {
            _loanService = loanService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _loanService.GetClientLoanDetailsAsync(id, currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
            }

            var vm = new LoanDetailsViewModel
            {
                Loan = _mapper.Map<LoanViewModel>(result.Value!.Loan),
                Installments = _mapper.Map<List<LoanInstallmentViewModel>>(result.Value.Installments)
            };

            return View(vm);
        }
    }
}
