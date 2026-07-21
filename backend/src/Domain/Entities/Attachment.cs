namespace Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;
    public string FileExtension { get; set; } = null!;

    public int FileSize { get; set; }

    public string StoragePath { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public Guid UploadedByEmployeeId { get; set; }

    public Expense? Expense { get; set; }
}
