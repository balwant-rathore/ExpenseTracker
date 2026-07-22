using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public class NotificationLogWriter : INotificationLogWriter
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public NotificationLogWriter(IOptions<NotificationOptions> options, IHostEnvironment environment)
    {
        _logFilePath = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, options.Value.LogDirectory, options.Value.LogFileName));
    }

    public async Task AppendAsync(string htmlEntry, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            await File.AppendAllTextAsync(_logFilePath, htmlEntry, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
