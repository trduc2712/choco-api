using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Primitives.Persistence;

public interface IRepository<TAggregateRoot> where TAggregateRoot : AggregateRoot
{
    Task<TAggregateRoot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(TAggregateRoot aggregateRoot);
    void Remove(TAggregateRoot aggregateRoot);
}
