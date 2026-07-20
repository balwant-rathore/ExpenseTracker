namespace Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string AttachmentsRootPath { get; set; } = "storage/attachments";
}
