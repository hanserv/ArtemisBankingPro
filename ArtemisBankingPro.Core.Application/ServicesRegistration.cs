using System.Reflection;
using ArtemisBankingPro.Core.Application.Behaviors;
using ArtemisBankingPro.Core.Application.Interfaces;
using ArtemisBankingPro.Core.Application.Services;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ArtemisBankingPro.Core.Application
{
    public static class ServicesRegistration
    {
        public static void AddApplicationLayer(this IServiceCollection services)
        {
            #region Mapster
            var config = new TypeAdapterConfig();
            config.Scan(Assembly.GetExecutingAssembly());

            services.AddSingleton(config);
            services.AddScoped<IMapper, ServiceMapper>();

            services.AddMediatR(opt => opt.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            #endregion

            #region Services
            services.AddScoped<IAccountNumberGenerator,AccountNumberGenerator>();
            services.AddScoped<ICardNumberGenerator,CardNumberGenerator>();
            services.AddScoped<ILoanNumberGenerator,LoanNumberGenerator>();
            services.AddScoped<ISavingsAccountService,SavingsAccountService>();
            services.AddScoped<IFinancialSummaryService,FinancialSummaryService>();
            services.AddScoped<ITransactionService,TransactionService>();
            services.AddScoped<ICreditCardService,CreditCardService>();
            services.AddScoped<ILoanService,LoanService>();
            services.AddScoped<IDashboardService,DashboardService>();
            services.AddScoped<IBeneficiaryService,BeneficiaryService>();
            services.AddScoped<IClientTransactionService,ClientTransactionService>();
            #endregion
        }
    }
}
