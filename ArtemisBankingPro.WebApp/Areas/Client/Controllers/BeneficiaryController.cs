using System.Security.Claims;
using ArtemisBankingPro.Core.Application.DTOs.Beneficiary;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.Beneficiary;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class BeneficiaryController : Controller
    {
        private readonly IBeneficiaryService _beneficiaryService;
        private readonly IMapper _mapper;

        public BeneficiaryController(IBeneficiaryService beneficiaryService, IMapper mapper)
        {
            _beneficiaryService = beneficiaryService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _beneficiaryService.GetByClientIdAsync(currentUserId);

            var vm = result.IsSuccess ? _mapper.Map<List<BeneficiaryViewModel>>(result.Value!) : [];

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
            }

            return View(vm);
        }

        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddBeneficiaryViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var dto = _mapper.Map<AddBeneficiaryDto>(vm);
            dto.ClientId = currentUserId;

            var result = await _beneficiaryService.AddAsync(dto);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Error!);
                return View(vm);
            }

            TempData["Message"] = result.Message;
            return RedirectToRoute(new { area = "Client", controller = "Beneficiary", action = "Index" });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _beneficiaryService.GetByIdAsync(id, currentUserId);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToRoute(new { area = "Client", controller = "Beneficiary", action = "Index" });
            }

            return View(_mapper.Map<DeleteBeneficiaryViewModel>(result.Value!));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(DeleteBeneficiaryViewModel vm)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _beneficiaryService.DeleteAsync(vm.Id, currentUserId);

            TempData[result.IsSuccess ? "Message" : "ErrorMessage"] = result.IsSuccess ? result.Message : result.Error;

            return RedirectToRoute(new { area = "Client", controller = "Beneficiary", action = "Index" });
        }
    }
}
