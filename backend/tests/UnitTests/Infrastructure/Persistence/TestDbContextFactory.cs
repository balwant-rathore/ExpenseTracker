using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure.Persistence;

internal static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;

        return new ApplicationDbContext(options);
    }
}
