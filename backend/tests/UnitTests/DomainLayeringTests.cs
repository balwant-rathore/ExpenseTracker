using System.Reflection;

namespace UnitTests;

public class DomainLayeringTests
{
    [Fact]
    public void Domain_HasNoReferencesToOtherSolutionProjects()
    {
        var domainAssembly = Assembly.Load("Domain");
        var solutionProjectNames = new[] { "Application", "Infrastructure", "Api", "Shared" };

        var referencedSolutionProjects = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && solutionProjectNames.Contains(name))
            .ToList();

        Assert.Empty(referencedSolutionProjects);
    }
}
