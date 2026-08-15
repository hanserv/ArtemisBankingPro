using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.Services;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "Cashier")]
    public class TransactionController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly IMapper _mapper;
        private readonly ILoanService _loanService;

        public TransactionController(ITransactionService transactionService, IMapper mapper, 
            ILoanService loanService)
        {
            _transactionService = transactionService;
            _mapper = mapper;
            _loanService = loanService;
        }

        public IActionResult Deposit()
        {
            return View(new DepositViewModel { AccountNumber = "", Amount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deposit(DepositViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var result = await _transactionService.ValidateDepositAsync(_mapper.Map<DepositDto>(vm));

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            var confirmVm = _mapper.Map<DepositConfirmationViewModel>(result.Value!);

            return View("DepositConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDeposit(DepositConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _transactionService.ConfirmDepositAsync(_mapper.Map<DepositConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Cashier", controller = "Transaction", action = "Deposit" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" });
        }

        public IActionResult Withdrawal()
        {
            return View(new WithdrawalViewModel { AccountNumber = "", Amount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdrawal(WithdrawalViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _transactionService.ValidateWithdrawalAsync(_mapper.Map<WithdrawalDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            var confirmVm = _mapper.Map<WithdrawalConfirmationViewModel>(result.Value!);

            return View("WithdrawalConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmWithdrawal(WithdrawalConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _transactionService.ConfirmWithdrawalAsync(_mapper.Map<WithdrawalConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Cashier", controller = "Transaction", action = "Withdrawal" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" });
        }

        public IActionResult CreditCardPayment()
        {
            return View(new CreditCardPaymentViewModel { SourceAccountNumber = "", CardNumber = "", Amount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreditCardPayment(CreditCardPaymentViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _transactionService.ValidateCreditCardPaymentAsync(_mapper.Map<CreditCardPaymentDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            var confirmVm = _mapper.Map<CreditCardPaymentConfirmationViewModel>(result.Value!);

            return View("CreditCardPaymentConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCreditCardPayment(CreditCardPaymentConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _transactionService.ConfirmCreditCardPaymentAsync(_mapper.Map<CreditCardPaymentConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Cashier", controller = "Transaction", action = "CreditCardPayment" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" });
        }

        public IActionResult ThirdPartyTransaction()
        {
            return View(new ThirdPartyTransactionViewModel { SourceAccountNumber = "", DestinationAccountNumber = "", Amount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThirdPartyTransaction(ThirdPartyTransactionViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _transactionService.ValidateThirdPartyTransactionAsync(_mapper.Map<ThirdPartyTransactionDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            var confirmVm = _mapper.Map<ThirdPartyTransactionConfirmationViewModel>(result.Value!);

            return View("ThirdPartyTransactionConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmThirdPartyTransaction(ThirdPartyTransactionConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _transactionService.ConfirmThirdPartyTransactionAsync(_mapper.Map<ThirdPartyTransactionConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Cashier", controller = "Transaction", action = "ThirdPartyTransaction" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" });
        }

        public IActionResult LoanPayment()
        {
            return View(new LoanPaymentViewModel { SourceAccountNumber = "", LoanNumber = "", Amount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoanPayment(LoanPaymentViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _loanService.ValidateLoanPaymentAsync(_mapper.Map<LoanPaymentDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            var confirmVm = _mapper.Map<LoanPaymentConfirmationViewModel>(result.Value!);

            return View("LoanPaymentConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmLoanPayment(LoanPaymentConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _loanService.ConfirmLoanPaymentAsync(_mapper.Map<LoanPaymentConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Cashier", controller = "Transaction", action = "LoanPayment" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" });
        }
    }
}
