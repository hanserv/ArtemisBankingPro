using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.SavingsAccount;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SavingsAccountController : Controller
    {
        private readonly ISavingsAccountService _savingsAccountService;
        private readonly IMapper _mapper;
        private readonly ITransactionService _transactionService;
        private readonly IAccountServiceForWebApp _accountService;

        public SavingsAccountController(ISavingsAccountService savingsAccountService, IMapper mapper,
            ITransactionService transactionService, IAccountServiceForWebApp accountService)
        {
            _savingsAccountService = savingsAccountService;
            _mapper = mapper;
            _transactionService = transactionService;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index(SavingsAccountFilterViewModel filter)
        {
            var filterDto = _mapper.Map<SavingsAccountFilterDto>(filter);

            var result = await _savingsAccountService.GetPagedAsync(filterDto);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "SavingsAccount", action = "Index" });
            }

            var vm = new SavingsAccountListViewModel
            {
                Filter = filter,
                Accounts = _mapper.Map<PagedResult<SavingsAccountViewModel>>(result.Value!)
            };

            return View(vm);
        }

        public async Task<IActionResult> Assign(string? identification)
        {
            var result = await _accountService.GetClientsForAssignmentAsync(identification);

            var vm = new AssignSelectClientViewModel
            {
                Identification = identification,
                Clients = result.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(result.Value!) : []
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignSelectClientViewModel vm)
        {
            var validation = await _savingsAccountService.ValidateClientForAssignmentAsync(vm.SelectedClientId);

            if (!validation.IsSuccess)
            {
                ModelState.AddModelError("", validation.Error!);
                var result = await _accountService.GetClientsForAssignmentAsync(vm.Identification);
                vm.Clients = result.IsSuccess ? _mapper.Map<List<ClientForAssignmentViewModel>>(result.Value!) : [];
                return View(vm);
            }

            return RedirectToRoute(new { area="Admin", controller = "SavingsAccount", action = "AssignAccount", clientId = vm.SelectedClientId });
        }

        public IActionResult AssignAccount(string clientId)
        {
            return View(new AssignAccountViewModel { ClientId = clientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAccount(AssignAccountViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _savingsAccountService.CreateSecondaryAccountAsync(vm.ClientId, vm.InitialBalance, currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "SavingsAccount", action = "Index" });
        }

        public async Task<IActionResult> Details(int id, int page = 1)
        {
            var accountResult = await _savingsAccountService.GetByIdAsync(id);

            if (!accountResult.IsSuccess)
            {
                TempData["ErrorMessage"] = accountResult.Error;
                return RedirectToRoute(new { area = "Admin", controller = "SavingsAccount", action = "Index" });
            }

            var transactionsResult = await _transactionService.GetAccountTransactionsAsync(id, page, pageSize: 20);

            var vm = new SavingsAccountDetailsViewModel
            {
                Account = _mapper.Map<SavingsAccountViewModel>(accountResult.Value!),
                Transactions = transactionsResult.IsSuccess
                    ? _mapper.Map<PagedResult<TransactionViewModel>>(transactionsResult.Value!)
                    : new PagedResult<TransactionViewModel> { Items = [], Page = page, PageSize = 20, TotalRecords = 0 }
            };

            return View(vm);
        }

        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _savingsAccountService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "SavingsAccount", action = "Index" });
            }

            var vm = new CancelSavingsAccountViewModel
            {
                Id = result.Value!.Id,
                AccountNumber = result.Value.AccountNumber
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(CancelSavingsAccountViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _savingsAccountService.CancelSecondaryAccountAsync(vm.Id, currentUserId);

            TempData[result.IsSuccess ? "Message" : "ErrorMessage"] = result.IsSuccess ? result.Message : result.Error;

            return RedirectToRoute(new { area = "Admin", controller = "SavingsAccount", action = "Index" });
        }
    }
}
