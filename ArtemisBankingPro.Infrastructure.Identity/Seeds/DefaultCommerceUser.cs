using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Entities;
using ArtemisBankingPro.Core.Domain.Interfaces;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtemisBankingPro.Infrastructure.Identity.Seeds
{
    public static class DefaultCommerceUser
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager, ICommerceRepository commerceRepository, ISavingsAccountService savingsAccountService)
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

                    var commerce = new Commerce
                    {
                        Id = 0,
                        Name = "Tienda Leo",
                        Email = "tiendaleoitla@email.com",
                        Description = "Tienda de leo",
                        PhoneNumber = "8495351134",
                        Rnc = "130145623",
                        IsActive = true,
                        CreatedByAdminId = "SYSTEM",
                        CreatedAt = DateTime.UtcNow
                    };

                    await commerceRepository.AddAsync(commerce);

                    user.CommerceId = commerce.Id;
                    await userManager.UpdateAsync(user);

                    await savingsAccountService.CreatePrincipalAccountAsync(user.Id, 100m);
                }
            }
        }
    }
}
