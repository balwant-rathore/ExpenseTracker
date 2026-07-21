using Api.Authentication;
using Api.Authorization;
using Application.Dashboard;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = AuthorizationPolicyNames.EmployeeOrManagerOrFinance)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var role = User.GetRole();
        var employeeId = User.GetEmployeeId();

        return role switch
        {
            EmployeeRole.Employee => Ok(await _dashboardService.GetEmployeeDashboardAsync(employeeId, cancellationToken)),
            EmployeeRole.Manager => Ok(await _dashboardService.GetManagerDashboardAsync(employeeId, cancellationToken)),
            EmployeeRole.Finance => Ok(await _dashboardService.GetFinanceDashboardAsync(cancellationToken)),
            _ => throw new InvalidOperationException(
                "Unreachable: the EmployeeOrManagerOrFinance policy restricts callers to Employee, Manager, or Finance."),
        };
    }
}
