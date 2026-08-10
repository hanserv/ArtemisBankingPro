using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.Services;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Identity.Seeds
{
    public static class DefaultClientUser
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager, ISavingsAccountService savingsAccountService)
        {
            var user = new AppUser
            {
                FirstName = "Carlos",
                LastName = "Caraballo",
                Identification = "00312345678",
                UserName = "client",
                Email = "client@email.com",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            if (!await userManager.Users.AnyAsync(u => u.UserName == user.UserName))
            {
                var result = await userManager.CreateAsync(user, "Password123$");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Client");

                    await savingsAccountService.CreatePrincipalAccountAsync(user.Id, 100m);
                }
            }
        }
    }
}
