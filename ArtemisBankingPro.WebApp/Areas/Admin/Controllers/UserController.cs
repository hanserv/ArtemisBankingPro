using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs;
using ArtemisBankingPro.Core.Application.DTOs.User;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.User;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly IAccountServiceForWebApp _accountService;
        private readonly IMapper _mapper;

        public UserController(IAccountServiceForWebApp accountService, IMapper mapper)
        {
            _accountService = accountService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index(UserFilterViewModel filter)
        {
            var result = await _accountService.GetAllUsersAsync(filter.Page, pageSize: 20, filter.Role?.ToString());

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "Home", action = "Index" });
            }

            var vm = new UserListViewModel
            {
                Filter = filter,
                Users = _mapper.Map<PagedResult<UserViewModel>>(result.Value!)
            };

            return View(vm);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            string origin = Request.Headers.Origin.ToString();

            var result = await _accountService.RegisterUserAsync(_mapper.Map<RegisterDto>(vm), role: vm.UserType.ToString(), origin: origin);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Admin", controller = "User", action = "Index" });
        }

        public async Task<IActionResult> Edit(string id)
        {
            var result = await _accountService.GetUserByIdAsync(id);

            if(!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "User", action = "Index" });
            }

            var vm = _mapper.Map<UpdateUserViewModel>(result.Value!);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateUserViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _accountService.EditUserAsync(_mapper.Map<UpdateUserDto>(vm), currentUserId);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;

            return RedirectToRoute(new { area = "Admin", controller = "User", action = "Index" });
        }

        public async Task<IActionResult> ChangeStatus(string id, bool isActive)
        {
            var result = await _accountService.GetUserByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Admin", controller = "User", action = "Index" });
            }

            var vm = new ChangeUserStatusViewModel
            {
                Id = result.Value!.Id,
                FullName = $"{result.Value.FirstName} {result.Value.LastName}",
                IsActive = isActive
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(ChangeUserStatusViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _accountService.ChangeUserStatusAsync(vm.Id, vm.IsActive, currentUserId);

            TempData[result.IsSuccess ? "Message" : "ErrorMessage"] = result.IsSuccess ? result.Message : result.Error;

            return RedirectToRoute(new { area = "Admin", controller = "User", action = "Index" });
        }
    }
}
