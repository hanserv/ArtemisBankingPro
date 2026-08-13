using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.Loan;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LoanController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly IMapper _mapper;
        private readonly IFinancialSummaryService _financialSummaryService;

        public LoanController(ILoanService loanService, IMapper mapper,
            IFinancialSummaryService financialSummaryService)
        {
            _loanService = loanService;
            _mapper = mapper;
            _financialSummaryService = financialSummaryService;
        }

        public async Task<IActionResult> Index(LoanFilterViewModel filter)
        {
            var filterDto = _mapper.Map<LoanFilterDto>(filter);

            var result = await _loanService.GetPagedAsync(filterDto);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "Index" });
            }

            var vm = new LoanListViewModel
            {
                Filter = filter,
                Loans = _mapper.Map<PagedResult<LoanViewModel>>(result.Value!)
            };

            return View(vm);
        }

        public async Task<IActionResult> Assign(string? identification)
        {
            var clientsResult = await _loanService.GetClientsEligibleForLoanAsync(identification);
            var averageDebt = await _financialSummaryService.GetSystemAverageDebtAsync();

            var vm = new AssignLoanSelectClientViewModel
            {
                Identification = identification,
                SystemAverageDebt = averageDebt,
                Clients = clientsResult.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(clientsResult.Value!) : []
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignLoanSelectClientViewModel vm)
        {
            var validation = await _loanService.ValidateClientForAssignmentAsync(vm.SelectedClientId);

            if (!validation.IsSuccess)
            {
                ModelState.AddModelError("", validation.Error!);
                var clientsResult = await _loanService.GetClientsEligibleForLoanAsync(vm.Identification);
                vm.SystemAverageDebt = await _financialSummaryService.GetSystemAverageDebtAsync();
                vm.Clients = clientsResult.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(clientsResult.Value!) : [];
                return View(vm);
            }

            return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "AssignLoan", clientId = vm.SelectedClientId });
        }

        public IActionResult AssignLoan(string clientId)
        {
            return View(new AssignLoanViewModel { ClientId = clientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignLoan(AssignLoanViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _mapper.Map<AssignLoanDto>(vm);
            dto.AdminId = currentUserId;

            var result = await _loanService.AssignAsync(dto);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            if (result.Value!.RequiresRiskConfirmation)
            {
                return View("RiskWarning", new LoanRiskWarningViewModel
                {
                    Loan = vm,
                    RiskType = result.Value.RiskWarning!.RiskType,
                    CurrentDebt = result.Value.RiskWarning.CurrentDebt,
                    ProjectedDebt = result.Value.RiskWarning.ProjectedDebt,
                    AverageDebt = result.Value.RiskWarning.AverageDebt
                });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "Index" });
        }

        public async Task<IActionResult> Details(int id)
        {
            var result = await _loanService.GetDetailsAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "Index" });
            }

            var vm = new LoanDetailsViewModel
            {
                Loan = _mapper.Map<LoanViewModel>(result.Value!.Loan),
                Installments = _mapper.Map<List<LoanInstallmentViewModel>>(result.Value.Installments)
            };

            return View(vm);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var result = await _loanService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "Index" });
            }

            var vm = new ModifyLoanRateViewModel
            {
                LoanId = result.Value!.Id,
                AnnualInterestRate = result.Value.AnnualInterestRate
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ModifyLoanRateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _mapper.Map<ModifyLoanRateDto>(vm);

            var result = await _loanService.ModifyRateAsync(dto, currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "Loan", action = "Index" });
        }
    }
}
