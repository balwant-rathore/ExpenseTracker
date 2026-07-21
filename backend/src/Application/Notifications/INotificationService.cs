using Domain.Entities;

namespace Application.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken);
}
