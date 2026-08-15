using ArtemisBankingPro.Core.Application.DTOs.Dashboard;
using ArtemisBankingPro.Core.Application.ViewModels.Dashboard;
using Mapster;

namespace ArtemisBankingPro.Core.Application.Mappings.DtosVms
{
    public class DashboardMappingConfig : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<AdminDashboardDto, AdminDashboardViewModel>();
            config.NewConfig<CashierDashboardDto, CashierDashboardViewModel>();
        }
    }
}
