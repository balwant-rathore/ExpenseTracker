namespace Application.Dashboard;

public record FinanceDashboardResponse(
    int TotalSubmitted,
    int Approved,
    int Reimbursed,
    int PendingApprovals,
    int PendingReimbursements);
