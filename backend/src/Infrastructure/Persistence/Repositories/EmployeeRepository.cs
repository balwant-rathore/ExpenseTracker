using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken)
    {
        return await DbContext.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber, cancellationToken);
    }

    public async Task<Employee?> GetByIdWithManagerAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return await DbContext.Employees
            .Include(e => e.Manager)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, cancellationToken);
    }

    public async Task<IReadOnlyList<Employee>> GetByRoleAsync(EmployeeRole role, CancellationToken cancellationToken)
    {
        return await DbContext.Employees
            .Where(e => e.Role == role && e.IsActive)
            .ToListAsync(cancellationToken);
    }
}
