using System.Security.Claims;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class SavingsAccountController : Controller
    {
        private readonly ISavingsAccountService _savingsAccountService;
        private readonly IMapper _mapper;

        public SavingsAccountController(ISavingsAccountService savingsAccountService, IMapper mapper)
        {
            _savingsAccountService = savingsAccountService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var accountResult = await _savingsAccountService.GetClientAccountByIdAsync(id, currentUserId);
            if (!accountResult.IsSuccess)
            {
                TempData["ErrorMessage"] = accountResult.Error;
                return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
            }

            var transactionsResult = await _savingsAccountService.GetClientAccountTransactionsAsync(id, currentUserId);

            var vm = new ClientSavingsAccountDetailsViewModel
            {
                Account = _mapper.Map<SavingsAccountViewModel>(accountResult.Value!),
                Transactions = transactionsResult.IsSuccess
                        ? _mapper.Map<List<TransactionViewModel>>(transactionsResult.Value!)
                        : []
            };

            return View(vm);
        }
    }
}
