namespace Infrastructure.BackgroundServices;

public interface IOrphanAttachmentSweeper
{
    Task SweepAsync(CancellationToken cancellationToken);
}
