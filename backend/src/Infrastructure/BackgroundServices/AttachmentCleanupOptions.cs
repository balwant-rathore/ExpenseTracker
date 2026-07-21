namespace Infrastructure.BackgroundServices;

public class AttachmentCleanupOptions
{
    public const string SectionName = "AttachmentCleanup";

    public int IntervalMinutes { get; set; } = 60;
    public int OrphanThresholdHours { get; set; } = 24;
}
