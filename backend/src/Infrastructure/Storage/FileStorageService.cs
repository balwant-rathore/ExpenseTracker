using Domain.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

public class FileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IOptions<StorageOptions> options, IHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.AttachmentsRootPath));
        _logger = logger;
    }

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var relativePath = $"{now:yyyy}/{now:MM}/{Guid.NewGuid()}{fileExtension}";
        var absolutePath = ToAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        var absolutePath = ToAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
        else
        {
            _logger.LogDebug("Attachment file already absent, skipping delete: {RelativePath}", relativePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        var absolutePath = ToAbsolutePath(relativePath);
        return Task.FromResult<Stream>(new FileStream(absolutePath, FileMode.Open, FileAccess.Read));
    }

    private string ToAbsolutePath(string relativePath)
    {
        var segments = relativePath.Split('/');
        return Path.Combine(_rootPath, Path.Combine(segments));
    }
}
