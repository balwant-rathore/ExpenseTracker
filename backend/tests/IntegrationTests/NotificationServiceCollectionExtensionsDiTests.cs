using Api.Extensions;
using Domain.Notifications;
using Domain.Repositories;
using Infrastructure.Notifications;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests;

public class NotificationServiceCollectionExtensionsDiTests
{
    [Fact]
    public void AddNotificationFoundation_RegistersAllNotificationServices_ResolvableFromDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment { ContentRootPath = Path.GetTempPath() });
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer("Server=unused;Database=unused;"));
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notification:LogDirectory"] = "unused",
                ["Notification:LogFileName"] = "unused.html",
                ["Notification:TimestampFormat"] = "yyyy-MM-dd HH:mm:ss",
            })
            .Build();

        services.AddNotificationFoundation(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationLogWriter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationService>());
        Assert.IsType<HtmlNotificationService>(scope.ServiceProvider.GetRequiredService<INotificationService>());

        var writer1 = provider.GetRequiredService<INotificationLogWriter>();
        var writer2 = provider.GetRequiredService<INotificationLogWriter>();
        Assert.Same(writer1, writer2);
    }
}
