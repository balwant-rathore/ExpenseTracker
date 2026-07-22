using System.Net;
using Domain.Entities;
using Domain.Notifications;

namespace Infrastructure.Notifications;

public static class NotificationTemplateRenderer
{
    public static (string Subject, string HtmlBody) Render(
        NotificationEvent notificationEvent, Expense expense, string employeeName, string? actorName)
    {
        var encodedEmployeeName = WebUtility.HtmlEncode(employeeName) ?? string.Empty;
        var encodedActorName = WebUtility.HtmlEncode(actorName ?? string.Empty);

        return notificationEvent switch
        {
            NotificationEvent.Submitted => (
                $"Expense {expense.ExpenseNumber} Submitted",
                Body(
                    ("Expense Number", expense.ExpenseNumber),
                    ("Employee", encodedEmployeeName),
                    ("Category", expense.Category.ToString()),
                    ("Amount", FormatAmount(expense)),
                    ("Submission Date", FormatDate(expense.SubmittedAt)))),

            NotificationEvent.Approved => (
                $"Expense {expense.ExpenseNumber} Approved",
                Body(
                    ("Expense Number", expense.ExpenseNumber),
                    ("Employee", encodedEmployeeName),
                    ("Category", expense.Category.ToString()),
                    ("Approved By", encodedActorName),
                    ("Approval Date", FormatDate(expense.ApprovedAt)))),

            NotificationEvent.Rejected or NotificationEvent.ComplianceRejected => (
                $"Expense {expense.ExpenseNumber} Rejected",
                Body(
                    ("Expense Number", expense.ExpenseNumber),
                    ("Employee", encodedEmployeeName),
                    ("Rejection Comment", WebUtility.HtmlEncode(expense.RejectionComment) ?? string.Empty),
                    ("Rejected By", encodedActorName),
                    ("Rejection Date", FormatDate(expense.RejectedAt)))),

            NotificationEvent.ComplianceApproved => (
                $"Expense {expense.ExpenseNumber} Compliance Approved",
                Body(
                    ("Expense Number", expense.ExpenseNumber),
                    ("Employee", encodedEmployeeName),
                    ("Category", expense.Category.ToString()),
                    ("Approved By", encodedActorName),
                    ("Approval Date", FormatDate(expense.ComplianceApprovedAt)))),

            NotificationEvent.Reimbursed => (
                $"Expense {expense.ExpenseNumber} Reimbursed",
                Body(
                    ("Expense Number", expense.ExpenseNumber),
                    ("Employee", encodedEmployeeName),
                    ("Amount", FormatAmount(expense)),
                    ("Reimbursement Date", FormatDate(expense.ReimbursedAt)))),

            _ => throw new ArgumentOutOfRangeException(nameof(notificationEvent), notificationEvent, null),
        };
    }

    private static string FormatAmount(Expense expense) => $"{expense.Amount} {expense.Currency}";

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? string.Empty;

    private static string Body(params (string Label, string Value)[] fields) =>
        string.Concat(fields.Select(f => $"<p><strong>{f.Label}:</strong> {f.Value}</p>"));
}
