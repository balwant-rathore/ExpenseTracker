namespace Infrastructure.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notification";

    public string LogDirectory { get; set; } = "storage/notifications";
    public string LogFileName { get; set; } = "notifications.html";
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss 'UTC'";
}
