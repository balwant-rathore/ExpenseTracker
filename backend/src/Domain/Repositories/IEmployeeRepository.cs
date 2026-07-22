using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken);
    Task<Employee?> GetByIdWithManagerAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Employee>> GetByRoleAsync(EmployeeRole role, CancellationToken cancellationToken);
}
