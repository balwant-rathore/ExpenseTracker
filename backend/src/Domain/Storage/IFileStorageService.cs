namespace Domain.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
}
