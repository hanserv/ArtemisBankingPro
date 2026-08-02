using ArtemisBankingPro.Infrastructure.Identity.Contexts;
using ArtemisBankingPro.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Infrastructure.Identity
{
    public static class ServicesRegistration
    {
        public static void AddIdentityLayerForWebApp(this IServiceCollection services, IConfiguration configuration)
        {
            GeneralConfiguration(services, configuration);

            #region Identity
            services.Configure<IdentityOptions>(opt =>
            {
                opt.Password.RequiredLength = 8;
                opt.Password.RequireUppercase = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireDigit = true;
                opt.Password.RequireNonAlphanumeric = true;

                opt.Lockout.AllowedForNewUsers = true;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opt.Lockout.MaxFailedAccessAttempts = 5;

                opt.User.RequireUniqueEmail = true;
                opt.SignIn.RequireConfirmedEmail = false;
            });

            services.AddIdentityCore<AppUser>()
                    .AddRoles<IdentityRole>()
                    .AddSignInManager()
                    .AddEntityFrameworkStores<IdentityContext>()
                    .AddTokenProvider<DataProtectorTokenProvider<AppUser>>(TokenOptions.DefaultProvider);

            services.Configure<DataProtectionTokenProviderOptions>(opt =>
            {
                opt.TokenLifespan = TimeSpan.FromMinutes(30);
            });

            services.AddAuthentication(opt =>
            {
                opt.DefaultScheme = IdentityConstants.ApplicationScheme;
                opt.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                opt.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            }).AddCookie(IdentityConstants.ApplicationScheme, opt =>
            {
                opt.ExpireTimeSpan = TimeSpan.FromMinutes(180);
                opt.LoginPath = "/Auth";
                opt.AccessDeniedPath = "/Auth/AccessDenied";
            });
            #endregion

            #region Services
            #endregion
        }

        // api layer

        #region Private Methods
        private static void GeneralConfiguration(IServiceCollection services, IConfiguration configuration)
        {
            #region Contexts
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                services.AddDbContext<IdentityContext>(opt => opt.UseInMemoryDatabase("AppDb"));
            }
            else
            {
                services.AddDbContext<IdentityContext>(opt =>
                    opt.UseSqlServer(configuration.GetConnectionString("IdentityConnection"),
                        m => m.MigrationsAssembly(typeof(IdentityContext).Assembly.FullName)));
            }
            #endregion
        }
        #endregion
    }
}
