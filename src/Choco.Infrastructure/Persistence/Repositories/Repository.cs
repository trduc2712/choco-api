using Choco.Domain.Primitives.Entities;
using Choco.Domain.Primitives.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal abstract class Repository<TAggregateRoot> : IRepository<TAggregateRoot>
    where TAggregateRoot : AggregateRoot
{
    protected ChocoDbContext DbContext { get; }

    protected DbSet<TAggregateRoot> DbSet { get; }

    protected Repository(ChocoDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<TAggregateRoot>();
    }

    public Task<TAggregateRoot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(aggregateRoot => aggregateRoot.Id == id, cancellationToken);
    }

    public void Add(TAggregateRoot aggregateRoot)
    {
        DbSet.Add(aggregateRoot);
    }

    public void Remove(TAggregateRoot aggregateRoot)
    {
        DbSet.Remove(aggregateRoot);
    }
}
