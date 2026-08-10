using Microsoft.AspNetCore.Identity;

namespace ArtemisBankingPro.Infrastructure.Identity.Seeds
{
    public static class DefaultRoles
    {
        public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Cashier"));
            await roleManager.CreateAsync(new IdentityRole("Client"));
            await roleManager.CreateAsync(new IdentityRole("Commerce"));
        }
    }
}
