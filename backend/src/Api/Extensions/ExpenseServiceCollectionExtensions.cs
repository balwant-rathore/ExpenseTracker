using Application.Expenses;

namespace Api.Extensions;

public static class ExpenseServiceCollectionExtensions
{
    public static IServiceCollection AddExpenseFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CompanyTimeZoneOptions>(configuration.GetSection(CompanyTimeZoneOptions.SectionName));

        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseNumberGenerator, ExpenseNumberGenerator>();
        services.AddScoped<ICompanyClock, CompanyClock>();

        return services;
    }
}
