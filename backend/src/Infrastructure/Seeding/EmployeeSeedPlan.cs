using Domain.Entities;

namespace Infrastructure.Seeding;

public sealed record EmployeeSeedPlan(
    IReadOnlyList<Employee> EmployeesToInsert,
    IReadOnlyList<string> UnresolvedManagerWarnings);
