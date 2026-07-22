using Application.Reports;
using Domain.Reporting;
using Infrastructure.Reporting;

namespace Api.Extensions;

public static class ReportServiceCollectionExtensions
{
    public static IServiceCollection AddReportFoundation(this IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IMonthlyReimbursementReportGenerator, ClosedXmlMonthlyReimbursementReportGenerator>();

        return services;
    }
}
