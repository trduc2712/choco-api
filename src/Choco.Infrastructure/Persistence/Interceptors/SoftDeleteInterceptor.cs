using Choco.Domain.Primitives.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Choco.Infrastructure.Persistence.Interceptors;

internal sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public SoftDeleteInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SoftDelete(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        SoftDelete(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SoftDelete(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        foreach (EntityEntry<ISoftDeletable> entry in dbContext.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = utcNow;
            }
        }
    }
}
