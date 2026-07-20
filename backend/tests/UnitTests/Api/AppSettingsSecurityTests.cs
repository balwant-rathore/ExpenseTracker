namespace UnitTests.Api;

public class AppSettingsSecurityTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void AppSettingsFile_DoesNotContainLiteralSigningKey(string fileName)
    {
        var backendRoot = FindBackendRoot(AppContext.BaseDirectory);
        var appSettingsPath = Path.Combine(backendRoot, "src", "Api", fileName);

        var content = File.ReadAllText(appSettingsPath);

        Assert.DoesNotContain("SigningKey", content, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindBackendRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ExpenseTracker.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate backend root (ExpenseTracker.sln) from test base directory.");
    }
}
