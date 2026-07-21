namespace Application.Dashboard;

public interface IDashboardService
{
    Task<EmployeeDashboardResponse> GetEmployeeDashboardAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ManagerDashboardResponse> GetManagerDashboardAsync(Guid managerId, CancellationToken cancellationToken);
    Task<FinanceDashboardResponse> GetFinanceDashboardAsync(CancellationToken cancellationToken);
}
