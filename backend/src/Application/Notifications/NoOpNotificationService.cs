using Domain.Entities;

namespace Application.Notifications;

public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
