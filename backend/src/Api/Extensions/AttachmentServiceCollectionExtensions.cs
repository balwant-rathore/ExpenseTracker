using Api.Authorization;
using Application.Attachments;
using Domain.Storage;
using Infrastructure.BackgroundServices;
using Infrastructure.Storage;

namespace Api.Extensions;

public static class AttachmentServiceCollectionExtensions
{
    public static IServiceCollection AddAttachmentFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<AttachmentCleanupOptions>(configuration.GetSection(AttachmentCleanupOptions.SectionName));

        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IOrphanAttachmentSweeper, OrphanAttachmentSweeper>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicyNames.EmployeeOrManager,
                p => p.RequireRole(AuthorizationPolicyNames.Employee, AuthorizationPolicyNames.Manager));
        });

        services.AddHostedService<OrphanAttachmentCleanupService>();

        return services;
    }
}
