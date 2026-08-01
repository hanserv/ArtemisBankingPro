using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Domain.Settings;
using ArtemisBankingPro.Infrastructure.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Infrastructure.Shared
{
    public static class ServicesRegistration
    {
        public static void AddSharedLayer(this IServiceCollection services, IConfiguration configuration)
        {
            #region Configurations
            services.Configure<MailSettings>(configuration.GetSection("MailSettings"));
            #endregion

            #region Services
            services.AddScoped<IEmailService, EmailService>();
            #endregion
        }
    }
}
