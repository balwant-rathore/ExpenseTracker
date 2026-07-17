using Domain.Entities;

namespace Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken);
}
