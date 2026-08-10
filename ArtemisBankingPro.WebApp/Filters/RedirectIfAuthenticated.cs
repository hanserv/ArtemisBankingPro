using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArtemisBankingPro.WebApp.Filters
{
    public class RedirectIfAuthenticated : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            if (context.HttpContext.User.Identity!.IsAuthenticated)
            {
                var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;

                context.Result = roleClaim switch
                {
                    "Admin" => new RedirectToRouteResult(new { area = "Admin", controller = "Home", action = "Index" }),
                    "Cashier" => new RedirectToRouteResult(new { area = "Cashier", controller = "Home", action = "Index" }),
                    "Client" => new RedirectToRouteResult(new { area = "Client", controller = "Home", action = "Index" }),
                    _ => new RedirectToRouteResult(new { area = "", controller = "Home", action = "Index" })
                };
            }

            base.OnActionExecuting(context);
        }
    }
}
