using Domain.Entities;
using Domain.Enums;
using Domain.Notifications;
using Infrastructure.Notifications;

namespace UnitTests.Infrastructure.Notifications;

public class NotificationTemplateRendererTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid AttachmentId = Guid.NewGuid();

    [Fact]
    public void Render_Submitted_IncludesExpenseNumberEmployeeCategoryAmountAndSubmissionDate()
    {
        var expense = CreateExpense();
        expense.SubmittedAt = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Submitted, expense, "Jane Doe", actorName: null);

        Assert.Contains("EXP-TEST-0001", subject);
        Assert.Contains("Submitted", subject);
        Assert.Contains("EXP-TEST-0001", body);
        Assert.Contains("Jane Doe", body);
        Assert.Contains("Travel", body);
        Assert.Contains("100", body);
        Assert.Contains("INR", body);
        Assert.Contains("2026-07-10", body);
    }

    [Fact]
    public void Render_Approved_IncludesCategoryApprovedByAndApprovalDate()
    {
        var expense = CreateExpense();
        expense.ApprovedAt = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Approved, expense, "Jane Doe", actorName: "Manager Mike");

        Assert.Contains("Approved", subject);
        Assert.Contains("Travel", body);
        Assert.Contains("Manager Mike", body);
        Assert.Contains("Approved By", body);
        Assert.Contains("2026-07-11", body);
    }

    [Fact]
    public void Render_ComplianceApproved_IncludesCategoryApprovedByAndApprovalDate()
    {
        var expense = CreateExpense(category: ExpenseCategory.ClientEntertainment);
        expense.ComplianceApprovedAt = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.ComplianceApproved, expense, "Jane Doe", actorName: "Compliance Carla");

        Assert.Contains("Compliance Approved", subject);
        Assert.Contains("ClientEntertainment", body);
        Assert.Contains("Compliance Carla", body);
        Assert.Contains("2026-07-12", body);
    }

    [Fact]
    public void Render_Rejected_IncludesRejectionCommentRejectedByAndRejectionDate()
    {
        var expense = CreateExpense();
        expense.RejectedAt = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
        expense.RejectionComment = "Missing itemized receipt";

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Rejected, expense, "Jane Doe", actorName: "Manager Mike");

        Assert.Contains("Rejected", subject);
        Assert.Contains("Missing itemized receipt", body);
        Assert.Contains("Manager Mike", body);
        Assert.Contains("2026-07-13", body);
    }

    [Fact]
    public void Render_ComplianceRejected_MatchesRejectedContentShape()
    {
        var expense = CreateExpense(category: ExpenseCategory.ClientEntertainment);
        expense.RejectedAt = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
        expense.RejectionComment = "Not a valid client entertainment expense";

        var rejected = NotificationTemplateRenderer.Render(
            NotificationEvent.Rejected, expense, "Jane Doe", actorName: "Compliance Carla");
        var complianceRejected = NotificationTemplateRenderer.Render(
            NotificationEvent.ComplianceRejected, expense, "Jane Doe", actorName: "Compliance Carla");

        Assert.Equal(rejected.Subject, complianceRejected.Subject);
        Assert.Equal(rejected.HtmlBody, complianceRejected.HtmlBody);
    }

    [Fact]
    public void Render_Reimbursed_IncludesAmountAndReimbursementDate()
    {
        var expense = CreateExpense();
        expense.ReimbursedAt = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Reimbursed, expense, "Jane Doe", actorName: null);

        Assert.Contains("Reimbursed", subject);
        Assert.Contains("100", body);
        Assert.Contains("INR", body);
        Assert.Contains("2026-07-15", body);
    }

    [Fact]
    public void Render_NeverContainsRawEmployeeOrExpenseGuid()
    {
        var expense = CreateExpense();
        expense.ApprovedAt = DateTime.UtcNow;

        var (subject, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Approved, expense, "Jane Doe", actorName: "Manager Mike");

        Assert.DoesNotContain(expense.Id.ToString(), subject);
        Assert.DoesNotContain(expense.Id.ToString(), body);
        Assert.DoesNotContain(expense.EmployeeId.ToString(), body);
    }

    [Fact]
    public void Render_HtmlEncodesRejectionCommentContainingMarkupCharacters()
    {
        var expense = CreateExpense();
        expense.RejectedAt = DateTime.UtcNow;
        expense.RejectionComment = "Amount <b>looks</b> wrong & receipt is blurry";

        var (_, body) = NotificationTemplateRenderer.Render(
            NotificationEvent.Rejected, expense, "Jane Doe", actorName: "Manager Mike");

        Assert.DoesNotContain("<b>looks</b>", body);
        Assert.Contains("&lt;b&gt;looks&lt;/b&gt;", body);
        Assert.Contains("&amp;", body);
    }

    private static Expense CreateExpense(ExpenseCategory category = ExpenseCategory.Travel) => new()
    {
        Id = Guid.NewGuid(),
        ExpenseNumber = "EXP-TEST-0001",
        EmployeeId = EmployeeId,
        AttachmentId = AttachmentId,
        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Category = category,
        Amount = 100m,
        Currency = "INR",
        Description = "Test expense",
        Status = ExpenseStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
