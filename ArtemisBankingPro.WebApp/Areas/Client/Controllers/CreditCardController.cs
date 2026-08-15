using System.Security.Claims;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.ViewModels.CreditCard;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.Areas.Client.Controllers
{
    [Area("Client")]
    [Authorize(Roles = "Client")]
    public class CreditCardController : Controller
    {
        private readonly ICreditCardService _creditCardService;
        private readonly IMapper _mapper;

        public CreditCardController(ICreditCardService creditCardService, IMapper mapper)
        {
            _creditCardService = creditCardService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var cardResult = await _creditCardService.GetClientCardByIdAsync(id, currentUserId);
            if (!cardResult.IsSuccess)
            {
                TempData["ErrorMessage"] = cardResult.Error;
                return RedirectToRoute(new { area = "Client", controller = "Home", action = "Index" });
            }

            var consumptionsResult = await _creditCardService.GetClientCardConsumptionsAsync(id, currentUserId);

            var vm = new CreditCardDetailsViewModel
            {
                Card = _mapper.Map<CreditCardViewModel>(cardResult.Value!),
                Consumptions = consumptionsResult.IsSuccess
                        ? _mapper.Map<List<CardConsumptionViewModel>>(consumptionsResult.Value!)
                        : []
            };

            return View(vm);
        }
    }
}
