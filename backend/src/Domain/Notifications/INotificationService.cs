using Domain.Entities;

namespace Domain.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken);
}
