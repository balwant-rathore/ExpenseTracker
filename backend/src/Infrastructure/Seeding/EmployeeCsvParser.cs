using Domain.Enums;

namespace Infrastructure.Seeding;

public class EmployeeCsvParser : IEmployeeCsvParser
{
    public async Task<IReadOnlyList<EmployeeCsvRow>> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var rows = new List<EmployeeCsvRow>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');

            var managerEmployeeNumber = fields[5].Trim();

            rows.Add(new EmployeeCsvRow(
                EmployeeNumber: fields[0].Trim(),
                FirstName: fields[1].Trim(),
                LastName: fields[2].Trim(),
                Email: fields[3].Trim(),
                Role: Enum.Parse<EmployeeRole>(fields[4].Trim()),
                ManagerEmployeeNumber: string.IsNullOrEmpty(managerEmployeeNumber) ? null : managerEmployeeNumber));
        }

        return rows;
    }
}
