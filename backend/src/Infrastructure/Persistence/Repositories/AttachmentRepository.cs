using Domain.Entities;
using Domain.Repositories;

namespace Infrastructure.Persistence.Repositories;

public class AttachmentRepository : Repository<Attachment>, IAttachmentRepository
{
    public AttachmentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public IQueryable<Attachment> Query()
    {
        return DbContext.Set<Attachment>().AsQueryable();
    }

    public void Remove(Attachment attachment)
    {
        DbContext.Set<Attachment>().Remove(attachment);
    }
}
