using System.Net;
using Domain.Entities;
using Domain.Enums;
using Domain.Notifications;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public class HtmlNotificationService : INotificationService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly INotificationLogWriter _logWriter;
    private readonly ILogger<HtmlNotificationService> _logger;
    private readonly string _timestampFormat;

    public HtmlNotificationService(
        IEmployeeRepository employeeRepository,
        INotificationLogWriter logWriter,
        IOptions<NotificationOptions> options,
        ILogger<HtmlNotificationService> logger)
    {
        _employeeRepository = employeeRepository;
        _logWriter = logWriter;
        _timestampFormat = options.Value.TimestampFormat;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationEvent notificationEvent, Expense expense, CancellationToken cancellationToken)
    {
        try
        {
            var owner = await _employeeRepository.GetByIdWithManagerAsync(expense.EmployeeId, cancellationToken);
            if (owner is null)
            {
                _logger.LogWarning(
                    "Notification skipped for {Event}: owning employee {EmployeeId} not found.",
                    notificationEvent, expense.EmployeeId);
                return;
            }

            var employeeName = $"{owner.FirstName} {owner.LastName}";

            string? actorName = null;
            var actorId = ResolveActorEmployeeId(notificationEvent, expense);
            if (actorId is not null)
            {
                var actor = await _employeeRepository.GetByIdAsync(actorId.Value, cancellationToken);
                actorName = actor is null ? null : $"{actor.FirstName} {actor.LastName}";
            }

            var cc = new List<string>();
            if (RequiresManagerCc(notificationEvent) && owner.Manager is not null)
            {
                cc.Add(owner.Manager.Email);
            }

            if (RequiresFinanceCc(notificationEvent))
            {
                var financeEmployees = await _employeeRepository.GetByRoleAsync(EmployeeRole.Finance, cancellationToken);
                cc.AddRange(financeEmployees.Select(e => e.Email));
            }

            var (subject, body) = NotificationTemplateRenderer.Render(notificationEvent, expense, employeeName, actorName);
            var entry = BuildLogEntry(notificationEvent, owner.Email, cc, subject, body);

            await _logWriter.AppendAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Failed to send {Event} notification for expense {ExpenseId}.", notificationEvent, expense.Id);
        }
    }

    private static bool RequiresManagerCc(NotificationEvent notificationEvent) =>
        notificationEvent is NotificationEvent.Submitted
            or NotificationEvent.Approved
            or NotificationEvent.ComplianceApproved
            or NotificationEvent.Reimbursed;

    private static bool RequiresFinanceCc(NotificationEvent notificationEvent) =>
        notificationEvent is NotificationEvent.Approved
            or NotificationEvent.ComplianceApproved
            or NotificationEvent.Reimbursed;

    private static Guid? ResolveActorEmployeeId(NotificationEvent notificationEvent, Expense expense) =>
        notificationEvent switch
        {
            NotificationEvent.Approved => expense.ApprovedByEmployeeId,
            NotificationEvent.Rejected => expense.RejectedByEmployeeId,
            NotificationEvent.ComplianceApproved => expense.ComplianceApprovedByEmployeeId,
            NotificationEvent.ComplianceRejected => expense.RejectedByEmployeeId,
            _ => null,
        };

    private string BuildLogEntry(
        NotificationEvent notificationEvent, string to, IReadOnlyList<string> cc, string subject, string body)
    {
        var timestamp = DateTime.UtcNow.ToString(_timestampFormat);
        var ccText = cc.Count == 0 ? string.Empty : string.Join(", ", cc);

        return $"""
            <div class="notification" data-event="{notificationEvent}">
            <p><strong>Timestamp:</strong> {WebUtility.HtmlEncode(timestamp)}</p>
            <p><strong>Event:</strong> {notificationEvent}</p>
            <p><strong>To:</strong> {WebUtility.HtmlEncode(to)}</p>
            <p><strong>CC:</strong> {WebUtility.HtmlEncode(ccText)}</p>
            <p><strong>Subject:</strong> {WebUtility.HtmlEncode(subject)}</p>
            <div><strong>Body:</strong>{body}</div>
            </div>
            <hr/>

            """;
    }
}
