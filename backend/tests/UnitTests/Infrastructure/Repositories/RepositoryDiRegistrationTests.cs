using Domain.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Infrastructure.Repositories;

public class RepositoryDiRegistrationTests
{
    [Fact]
    public void Repositories_And_UnitOfWork_ResolveFromDi()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer("Server=unused;Database=unused;"));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetOtpRepository, PasswordResetOtpRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmployeeRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPasswordResetOtpRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExpenseRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAttachmentRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }
}
