using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs.Transaction;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Beneficiary;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using ArtemisBankingPro.Core.Application.ViewModels.Loan;
using ArtemisBankingPro.Core.Application.ViewModels.SavingsAccount;
using ArtemisBankingPro.Core.Application.ViewModels.Transaction;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class TransactionController : Controller
    {
        private readonly IClientTransactionService _clientTransactionService;
        private readonly ISavingsAccountService _savingsAccountService;
        private readonly IMapper _mapper;
        private readonly ICreditCardService _creditCardService;
        private readonly ILoanService _loanService;
        private readonly IBeneficiaryService _beneficiaryService;

        public TransactionController(IClientTransactionService clientTransactionService, ISavingsAccountService savingsAccountService,
            IMapper mapper, ICreditCardService creditCardService,
            ILoanService loanService, IBeneficiaryService beneficiaryService)
        {
            _clientTransactionService = clientTransactionService;
            _savingsAccountService = savingsAccountService;
            _mapper = mapper;
            _creditCardService = creditCardService;
            _loanService = loanService;
            _beneficiaryService = beneficiaryService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Express()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var vm = new ExpressTransactionViewModel
            {
                SourceAccountNumber = "",
                DestinationAccountNumber = "",
                Amount = 0
            };

            await PopulateSourceAccountOptionsAsync(vm, currentUserId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Express(ExpressTransactionViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateSourceAccountOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var result = await _clientTransactionService.ValidateExpressTransactionAsync(_mapper.Map<ExpressTransactionDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateSourceAccountOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var confirmVm = _mapper.Map<ExpressTransactionConfirmationViewModel>(result.Value!);

            return View("ExpressTransactionConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmExpress(ExpressTransactionConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _clientTransactionService.ConfirmExpressTransactionAsync(_mapper.Map<ExpressTransactionConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Client", controller = "Transaction", action = "Express" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        public async Task<IActionResult> CreditCardPayment()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var vm = new ClientCreditCardPaymentViewModel
            {
                CreditCardId = 0,
                SourceAccountNumber = "",
                Amount = 0
            };

            await PopulateCreditCardPaymentOptionsAsync(vm, currentUserId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreditCardPayment(ClientCreditCardPaymentViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateCreditCardPaymentOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var result = await _clientTransactionService.PayCreditCardAsync(_mapper.Map<ClientCreditCardPaymentDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateCreditCardPaymentOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        public async Task<IActionResult> LoanPayment()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var vm = new ClientLoanPaymentViewModel
            {
                LoanId = 0,
                SourceAccountNumber = "",
                Amount = 0
            };

            await PopulateLoanPaymentOptionsAsync(vm, currentUserId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoanPayment(ClientLoanPaymentViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateLoanPaymentOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var dto = _mapper.Map<ClientLoanPaymentDto>(vm);
            var result = await _loanService.PayLoanAsync(dto, currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateLoanPaymentOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        public async Task<IActionResult> BeneficiaryTransaction()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var vm = new BeneficiaryTransactionViewModel
            {
                BeneficiaryId = 0,
                SourceAccountNumber = "",
                Amount = 0
            };

            await PopulateBeneficiaryTransactionOptionsAsync(vm, currentUserId);

            if (vm.BeneficiaryOptions.Count == 0)
            {
                TempData["ErrorMessage"] = "You do not have any registered beneficiaries.";
                return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BeneficiaryTransaction(BeneficiaryTransactionViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateBeneficiaryTransactionOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var result = await _clientTransactionService.ValidateBeneficiaryTransactionAsync(_mapper.Map<BeneficiaryTransactionDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateBeneficiaryTransactionOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var confirmVm = _mapper.Map<BeneficiaryTransactionConfirmationViewModel>(result.Value!);

            return View("BeneficiaryTransactionConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBeneficiaryTransaction(BeneficiaryTransactionConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            var result = await _clientTransactionService.ConfirmBeneficiaryTransactionAsync(_mapper.Map<BeneficiaryTransactionConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Client", controller = "Transaction", action = "BeneficiaryTransaction" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        public async Task<IActionResult> OwnAccountTransfer()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(currentUserId);
            var accountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];

            if (accountOptions.Count < 2)
            {
                TempData["ErrorMessage"] = "You must have at least two active savings accounts to make a transfer between accounts.";
                return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
            }

            var vm = new OwnAccountTransferViewModel
            {
                SourceAccountNumber = "",
                DestinationAccountNumber = "",
                Amount = 0,
                AccountOptions = accountOptions
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OwnAccountTransfer(OwnAccountTransferViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateOwnAccountTransferOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var result = await _clientTransactionService.ValidateOwnAccountTransferAsync(_mapper.Map<OwnAccountTransferDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateOwnAccountTransferOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var confirmVm = _mapper.Map<OwnAccountTransferConfirmationViewModel>(result.Value!);

            return View("OwnAccountTransferConfirmation", confirmVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOwnAccountTransfer(OwnAccountTransferConfirmationViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _clientTransactionService.ConfirmOwnAccountTransferAsync(_mapper.Map<OwnAccountTransferConfirmationDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Client", controller = "Transaction", action = "OwnAccountTransfer" });
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        public async Task<IActionResult> CashAdvance()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var vm = new CashAdvanceViewModel
            {
                CreditCardId = 0,
                DestinationAccountNumber = "",
                Amount = 0
            };

            await PopulateCashAdvanceOptionsAsync(vm, currentUserId);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashAdvance(CashAdvanceViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!ModelState.IsValid)
            {
                await PopulateCashAdvanceOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            var result = await _clientTransactionService.RequestCashAdvanceAsync(_mapper.Map<CashAdvanceDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                await PopulateCashAdvanceOptionsAsync(vm, currentUserId);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
        }

        #region Private Methods

        private async Task PopulateSourceAccountOptionsAsync(ExpressTransactionViewModel vm, string clientId)
        {
            var result = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);

            vm.SourceAccountOptions = result.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(result.Value!) : [];
        }

        private async Task PopulateCreditCardPaymentOptionsAsync(ClientCreditCardPaymentViewModel vm, string clientId)
        {
            var cardsResult = await _creditCardService.GetActiveCardsByClientIdAsync(clientId);
            vm.CardOptions = cardsResult.IsSuccess ? _mapper.Map<List<CreditCardViewModel>>(cardsResult.Value!) : [];

            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);
            vm.SourceAccountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];
        }

        private async Task PopulateLoanPaymentOptionsAsync(ClientLoanPaymentViewModel vm, string clientId)
        {
            var loansResult = await _loanService.GetActiveLoansByClientIdAsync(clientId);
            vm.LoanOptions = loansResult.IsSuccess ? _mapper.Map<List<LoanViewModel>>(loansResult.Value!) : [];

            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);
            vm.SourceAccountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];
        }

        private async Task PopulateBeneficiaryTransactionOptionsAsync(BeneficiaryTransactionViewModel vm, string clientId)
        {
            var beneficiariesResult = await _beneficiaryService.GetByClientIdAsync(clientId);
            vm.BeneficiaryOptions = beneficiariesResult.IsSuccess ? _mapper.Map<List<BeneficiaryViewModel>>(beneficiariesResult.Value!) : [];

            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);
            vm.SourceAccountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];
        }

        private async Task PopulateOwnAccountTransferOptionsAsync(OwnAccountTransferViewModel vm, string clientId)
        {
            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);
            vm.AccountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];
        }

        private async Task PopulateCashAdvanceOptionsAsync(CashAdvanceViewModel vm, string clientId)
        {
            var cardsResult = await _creditCardService.GetActiveCardsByClientIdAsync(clientId);
            vm.CardOptions = cardsResult.IsSuccess ? _mapper.Map<List<CreditCardViewModel>>(cardsResult.Value!) : [];

            var accountsResult = await _savingsAccountService.GetActiveAccountsByClientIdAsync(clientId);
            vm.AccountOptions = accountsResult.IsSuccess ? _mapper.Map<List<SavingsAccountViewModel>>(accountsResult.Value!) : [];
        }

        #endregion
    }
}
