using Domain.Enums;

namespace Api.Authorization;

public static class AuthorizationPolicyNames
{
    public const string Employee = nameof(EmployeeRole.Employee);
    public const string Manager = nameof(EmployeeRole.Manager);
    public const string Finance = nameof(EmployeeRole.Finance);
    public const string ComplianceOfficer = nameof(EmployeeRole.ComplianceOfficer);
    public const string EmployeeOrManager = "EmployeeOrManager";
    public const string EmployeeOrManagerOrFinance = "EmployeeOrManagerOrFinance";
}
