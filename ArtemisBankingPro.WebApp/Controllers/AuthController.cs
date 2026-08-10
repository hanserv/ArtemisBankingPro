using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.User;
using ArtemisBankingPro.WebApp.Filters;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly IAccountServiceForWebApp _accountService;
        private readonly IMapper _mapper;

        public AuthController(IAccountServiceForWebApp accountService, IMapper mapper)
        {
            _accountService = accountService;
            _mapper = mapper;
        }

        [RedirectIfAuthenticated]
        public IActionResult Login()
        {
            if (TempData.TryGetValue("LoginError", out var error) && error is string errorMessage)
            {
                ModelState.AddModelError("", errorMessage);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var result = await _accountService.AuthenticateAsync(_mapper.Map<LoginDto>(vm));

            if (!result.IsSuccess)
            {
                TempData["LoginError"] = result.Error;
                return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
            }

            TempData["Message"] = result.Message;

            return result.Value!.Role switch
            {
                "Admin" => RedirectToRoute(new { area = "Admin", controller = "Home", action = "Index" }),
                "Cashier" => RedirectToRoute(new { area = "Cashier", controller = "Home", action = "Index" }),
                "Client" => RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" }),
                _ => RedirectToRoute(new { area = "", controller = "Auth", action = "Login" })
            };
        }

        [RedirectIfAuthenticated]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var result = await _accountService.ConfirmAccountAsync(userId, token);

            TempData[result.IsSuccess ? "Message" : "ErrorMessage"] = result.IsSuccess ? result.Message : result.Error;

            return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
        }

        [RedirectIfAuthenticated]
        public IActionResult RequestPasswordReset()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPasswordReset(RequestPasswordResetViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            string origin = Request.Headers.Origin.ToString();

            var result = await _accountService.RequestPasswordResetAsync(_mapper.Map<RequestPasswordResetDto>(vm), origin, isApi: false);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
        }

        [RedirectIfAuthenticated]
        public IActionResult ResetPassword(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
            }

            return View(new ResetPasswordRequestViewModel { UserId = userId, Token = token, Password = "", ConfirmPassword = "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequestViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            string origin = Request.Headers.Origin.ToString();

            var result = await _accountService.ResetPasswordAsync(_mapper.Map<ResetPasswordDto>(vm));

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOut()
        {
            await _accountService.SignOutAsync();
            return RedirectToRoute(new { area = "", controller = "Auth", action = "Login" });
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
