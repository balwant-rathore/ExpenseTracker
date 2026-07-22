namespace Infrastructure.Notifications;

public interface INotificationLogWriter
{
    Task AppendAsync(string htmlEntry, CancellationToken cancellationToken);
}
