using Choco.Domain.Primitives.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Choco.Infrastructure.Persistence.Interceptors;

internal sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public AuditInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetAuditValues(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        SetAuditValues(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetAuditValues(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        foreach (EntityEntry<IAuditable> entry in dbContext.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = utcNow;
                    entry.Property(nameof(IAuditable.ModifiedAt)).CurrentValue = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.ModifiedAt)).CurrentValue = utcNow;
                    break;
            }
        }
    }
}
