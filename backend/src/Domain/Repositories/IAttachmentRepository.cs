using Domain.Entities;

namespace Domain.Repositories;

public interface IAttachmentRepository : IRepository<Attachment>
{
    IQueryable<Attachment> Query();
    void Remove(Attachment attachment);
}
