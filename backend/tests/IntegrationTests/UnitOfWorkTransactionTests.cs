using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class UnitOfWorkTransactionTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private Guid _employeeId;

    public UnitOfWorkTransactionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..9];
        var employee = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = $"TX-{suffix}",
            FirstName = "Test",
            LastName = "User",
            Email = $"{suffix}@test.local",
            Role = EmployeeRole.Employee,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        _employeeId = employee.EmployeeId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Users.RemoveRange(dbContext.Users.Where(u => u.EmployeeId == _employeeId));
        await dbContext.SaveChangesAsync();

        dbContext.Employees.RemoveRange(dbContext.Employees.Where(e => e.EmployeeId == _employeeId));
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ExceptionAfterInnerSaveChanges_RollsBackTheInsertedRow()
    {
        using var scope = _factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                var user = new User
                {
                    Id = userId,
                    EmployeeId = _employeeId,
                    Email = "rollback-test@test.local",
                    NormalizedEmail = "ROLLBACK-TEST@TEST.LOCAL",
                    PasswordHash = "unused",
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync(CancellationToken.None);

                throw new InvalidOperationException("Simulated failure after the inner SaveChangesAsync.");
            },
            CancellationToken.None));

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verifyDbContext.Users.FindAsync(userId);

        Assert.Null(persisted);
    }
}
