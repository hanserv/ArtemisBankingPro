using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Identity.Seeds
{
    public static class DefaultAdminUser
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager)
        {
            var user = new AppUser
            {
                FirstName = "Leonardo",
                LastName = "Tavarez",
                Identification = "001-1234567-8",
                UserName = "admin",
                Email = "admin@email.com",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            if (!await userManager.Users.AnyAsync(u => u.UserName == user.UserName))
            {
                var result = await userManager.CreateAsync(user, "Password123$");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }
        }
    }
}
