using Domain.Notifications;
using Infrastructure.Notifications;

namespace Api.Extensions;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));

        services.AddSingleton<INotificationLogWriter, NotificationLogWriter>();
        services.AddScoped<INotificationService, HtmlNotificationService>();

        return services;
    }
}
