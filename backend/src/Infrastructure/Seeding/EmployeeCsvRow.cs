using Domain.Enums;

namespace Infrastructure.Seeding;

public sealed record EmployeeCsvRow(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    string? ManagerEmployeeNumber);
