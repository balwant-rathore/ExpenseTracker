using Api.Authorization;
using Application.Dashboard;

namespace Api.Extensions;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardFoundation(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicyNames.EmployeeOrManagerOrFinance,
                p => p.RequireRole(
                    AuthorizationPolicyNames.Employee,
                    AuthorizationPolicyNames.Manager,
                    AuthorizationPolicyNames.Finance));
        });

        return services;
    }
}
