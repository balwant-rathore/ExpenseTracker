using Infrastructure.Notifications;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure.Notifications;

public class NotificationLogWriterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _logFilePath;
    private readonly NotificationLogWriter _writer;

    public NotificationLogWriterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "et015-notification-tests", Guid.NewGuid().ToString("N"));
        var environment = new FakeHostEnvironment { ContentRootPath = _tempRoot };
        var options = Options.Create(new NotificationOptions
        {
            LogDirectory = "notifications",
            LogFileName = "notifications.html",
        });
        _logFilePath = Path.Combine(_tempRoot, "notifications", "notifications.html");
        _writer = new NotificationLogWriter(options, environment);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AppendAsync_LogFileDoesNotExist_CreatesItBeforeWriting()
    {
        Assert.False(File.Exists(_logFilePath));

        await _writer.AppendAsync("<div>first entry</div>", CancellationToken.None);

        Assert.True(File.Exists(_logFilePath));
        Assert.Contains("first entry", await File.ReadAllTextAsync(_logFilePath));
    }

    [Fact]
    public async Task AppendAsync_CalledTwice_AppendsWithoutTruncatingExistingContent()
    {
        await _writer.AppendAsync("<div>first entry</div>", CancellationToken.None);
        await _writer.AppendAsync("<div>second entry</div>", CancellationToken.None);

        var content = await File.ReadAllTextAsync(_logFilePath);
        Assert.Contains("first entry", content);
        Assert.Contains("second entry", content);
        Assert.True(content.IndexOf("first entry", StringComparison.Ordinal) < content.IndexOf("second entry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AppendAsync_TwoConcurrentCalls_BothEntriesAreWellFormedAndNotInterleaved()
    {
        var first = _writer.AppendAsync("<div class=\"a\">AAAAAAAAAA</div>", CancellationToken.None);
        var second = _writer.AppendAsync("<div class=\"b\">BBBBBBBBBB</div>", CancellationToken.None);

        await Task.WhenAll(first, second);

        var content = await File.ReadAllTextAsync(_logFilePath);
        Assert.Contains("<div class=\"a\">AAAAAAAAAA</div>", content);
        Assert.Contains("<div class=\"b\">BBBBBBBBBB</div>", content);
    }
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";

    public IFileProvider ContentRootFileProvider { get; set; } = null!;

    public string ContentRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = "Test";
}
