using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.CreditCard;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using ArtemisBankingPro.Core.Domain.Common.Enums;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CreditCardController : Controller
    {
        private readonly ICreditCardService _creditCardService;
        private readonly IMapper _mapper;
        private readonly IAccountServiceForWebApp _accountService;
        private readonly IFinancialSummaryService _financialSummaryService;

        public CreditCardController(ICreditCardService creditCardService, IMapper mapper,
            IAccountServiceForWebApp accountService, IFinancialSummaryService financialSummaryService)
        {
            _creditCardService = creditCardService;
            _mapper = mapper;
            _accountService = accountService;
            _financialSummaryService = financialSummaryService;
        }

        public async Task<IActionResult> Index(CreditCardFilterViewModel filter)
        {
            if (!Request.QueryString.HasValue)
            {
                filter.Status = CreditCardStatus.Active;
            }

            var filterDto = _mapper.Map<CreditCardFilterDto>(filter);

            var result = await _creditCardService.GetPagedAsync(filterDto);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
            }

            var vm = new CreditCardListViewModel
            {
                Filter = filter,
                Cards = _mapper.Map<PagedResult<CreditCardViewModel>>(result.Value!)
            };

            return View(vm);
        }

        public async Task<IActionResult> Assign(string? identification)
        {
            var clientsResult = await _accountService.GetClientsForAssignmentAsync(identification);
            var averageDebt = await _financialSummaryService.GetSystemAverageDebtAsync();

            var vm = new AssignCreditCardSelectClientViewModel
            {
                Identification = identification,
                SystemAverageDebt = averageDebt,
                Clients = clientsResult.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(clientsResult.Value!) : []
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignCreditCardSelectClientViewModel vm)
        {
            var validation = await _creditCardService.ValidateClientForAssignmentAsync(vm.SelectedClientId);

            if (!validation.IsSuccess)
            {
                ModelState.AddModelError("", validation.Error!);
                var clientsResult = await _accountService.GetClientsForAssignmentAsync(vm.Identification);
                vm.SystemAverageDebt = await _financialSummaryService.GetSystemAverageDebtAsync();
                vm.Clients = clientsResult.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(clientsResult.Value!) : [];
                return View(vm);
            }

            return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "AssignCard", clientId = vm.SelectedClientId });
        }

        public IActionResult AssignCard(string clientId)
        {
            return View(new AssignCreditCardViewModel { ClientId = clientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCard(AssignCreditCardViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _mapper.Map<AssignCreditCardDto>(vm);

            var result = await _creditCardService.AssignCreditCardAsync(dto, currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
        }

        public async Task<IActionResult> Details(int id)
        {
            var cardResult = await _creditCardService.GetByIdAsync(id);

            if (!cardResult.IsSuccess)
            {
                TempData["ErrorMessage"] = cardResult.Error;
                return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
            }

            var consumptionsResult = await _creditCardService.GetConsumptionsAsync(id);

            var vm = new CreditCardDetailsViewModel
            {
                Card = _mapper.Map<CreditCardViewModel>(cardResult.Value!),
                Consumptions = consumptionsResult.IsSuccess
                    ? _mapper.Map<List<CardConsumptionViewModel>>(consumptionsResult.Value!)
                    : []
            };

            return View(vm);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var result = await _creditCardService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
            }

            var vm = new ModifyCreditCardLimitViewModel
            {
                CreditCardId = result.Value!.Id,
                CreditLimit = result.Value.CreditLimit
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ModifyCreditCardLimitViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _mapper.Map<ModifyCreditCardLimitDto>(vm);

            var result = await _creditCardService.ModifyCreditCardLimitAsync(dto, currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
        }

        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _creditCardService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
            }

            var vm = _mapper.Map<CancelCreditCardViewModel>(result.Value!);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(CancelCreditCardViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _creditCardService.CancelCreditCardAsync(vm.Id, currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "CreditCard", action = "Index" });
        }
    }
}
