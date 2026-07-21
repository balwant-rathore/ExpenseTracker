using Application.Reports;

namespace Api.Extensions;

public static class ReportServiceCollectionExtensions
{
    public static IServiceCollection AddReportFoundation(this IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
