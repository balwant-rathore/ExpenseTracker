using Domain.Entities;

namespace Infrastructure.Seeding;

public sealed class EmployeeSeedPlanner
{
    public EmployeeSeedPlan BuildPlan(
        IReadOnlyList<EmployeeCsvRow> csvRows,
        IReadOnlyDictionary<string, Guid> existingEmployeesByNumber)
    {
        var newRows = csvRows
            .Where(row => !existingEmployeesByNumber.ContainsKey(row.EmployeeNumber))
            .ToList();

        var now = DateTime.UtcNow;
        var newEmployeesByNumber = new Dictionary<string, Employee>();

        foreach (var row in newRows)
        {
            newEmployeesByNumber[row.EmployeeNumber] = new Employee
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeNumber = row.EmployeeNumber,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                Role = row.Role,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        var combinedEmployeeNumberToId = new Dictionary<string, Guid>(existingEmployeesByNumber);
        foreach (var (employeeNumber, employee) in newEmployeesByNumber)
        {
            combinedEmployeeNumberToId[employeeNumber] = employee.EmployeeId;
        }

        var warnings = new List<string>();

        foreach (var row in newRows)
        {
            if (string.IsNullOrEmpty(row.ManagerEmployeeNumber))
            {
                continue;
            }

            if (combinedEmployeeNumberToId.TryGetValue(row.ManagerEmployeeNumber, out var managerId))
            {
                newEmployeesByNumber[row.EmployeeNumber].ManagerId = managerId;
            }
            else
            {
                warnings.Add(
                    $"Employee '{row.EmployeeNumber}' references unknown Manager '{row.ManagerEmployeeNumber}'.");
            }
        }

        return new EmployeeSeedPlan(newEmployeesByNumber.Values.ToList(), warnings);
    }
}
