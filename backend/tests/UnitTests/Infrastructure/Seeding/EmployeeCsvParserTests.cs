using Domain.Enums;
using Infrastructure.Seeding;

namespace UnitTests.Infrastructure.Seeding;

public class EmployeeCsvParserTests
{
    [Fact]
    public async Task ParseAsync_ParsesSixColumnFormat_IncludingBlankManagerColumn()
    {
        var csvPath = Path.GetTempFileName();
        await File.WriteAllLinesAsync(csvPath, new[]
        {
            "Employee Number,First Name,Last Name,Email,Role,Manager",
            "EMP001,Sarah,Johnson,sarah.johnson@company.com,ComplianceOfficer,",
            "EMP015,Noah,Walker,noah.walker@company.com,Employee,EMP005"
        });

        try
        {
            var parser = new EmployeeCsvParser();
            var rows = await parser.ParseAsync(csvPath, CancellationToken.None);

            Assert.Equal(2, rows.Count);

            var withoutManager = rows[0];
            Assert.Equal("EMP001", withoutManager.EmployeeNumber);
            Assert.Equal("Sarah", withoutManager.FirstName);
            Assert.Equal("Johnson", withoutManager.LastName);
            Assert.Equal("sarah.johnson@company.com", withoutManager.Email);
            Assert.Equal(EmployeeRole.ComplianceOfficer, withoutManager.Role);
            Assert.Null(withoutManager.ManagerEmployeeNumber);

            var withManager = rows[1];
            Assert.Equal("EMP015", withManager.EmployeeNumber);
            Assert.Equal(EmployeeRole.Employee, withManager.Role);
            Assert.Equal("EMP005", withManager.ManagerEmployeeNumber);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }
}
