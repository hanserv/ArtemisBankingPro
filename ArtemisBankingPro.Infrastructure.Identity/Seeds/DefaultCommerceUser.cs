using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Identity.Seeds
{
    public static class DefaultCommerceUser
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager)
        {
            var user = new AppUser
            {
                FirstName = "Juan",
                LastName = "Rosario",
                Identification = "00412345678",
                UserName = "commerce",
                Email = "commerce@email.com",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            if (!await userManager.Users.AnyAsync(u => u.UserName == user.UserName))
            {
                var result = await userManager.CreateAsync(user, "Password123$");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Commerce");
                }
            }

            // TODO (Doc. pág. 158): asociar este usuario a un registro de Commerce (Commerce.UserId).
        }
    }
}
