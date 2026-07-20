using Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure.Storage;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileStorageService _service;

    public FileStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "et006-filestorage-tests", Guid.NewGuid().ToString("N"));
        var environment = new FakeHostEnvironment { ContentRootPath = _tempRoot };
        var options = Options.Create(new StorageOptions { AttachmentsRootPath = "attachments" });
        _service = new FileStorageService(options, environment, NullLogger<FileStorageService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesUnderConfiguredRoot_UsingYearMonthGuidPath()
    {
        var now = DateTime.UtcNow;

        var relativePath = await _service.SaveAsync(new MemoryStream([1, 2, 3, 4]), ".pdf", CancellationToken.None);

        Assert.StartsWith($"{now:yyyy}/{now:MM}/", relativePath);
        Assert.EndsWith(".pdf", relativePath);

        var absolutePath = Path.Combine(_tempRoot, "attachments", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolutePath));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(absolutePath));
    }

    [Fact]
    public async Task DeleteAsync_FileDoesNotExist_DoesNotThrow()
    {
        await _service.DeleteAsync("2026/07/does-not-exist.pdf", CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        var relativePath = await _service.SaveAsync(new MemoryStream([9]), ".png", CancellationToken.None);
        var absolutePath = Path.Combine(_tempRoot, "attachments", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolutePath));

        await _service.DeleteAsync(relativePath, CancellationToken.None);

        Assert.False(File.Exists(absolutePath));
    }
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";

    public IFileProvider ContentRootFileProvider { get; set; } = null!;

    public string ContentRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = "Test";
}
