using Microsoft.AspNetCore.Mvc;

namespace ArtemisBankingPro.WebApp.ViewComponents
{
    public class MenuViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View();
        }
    }
}
