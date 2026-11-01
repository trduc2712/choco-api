using System.Linq.Expressions;

using Choco.Application.Abstractions;
using Choco.Domain.Primitives.Entities;
using Choco.Domain.Primitives.Events;
using Choco.Domain.Primitives.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Choco.Infrastructure.Persistence;

internal sealed class ChocoDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher _publisher;

    public ChocoDbContext(DbContextOptions<ChocoDbContext> options, IPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            IgnoreDomainEvents(modelBuilder, entityType);
            ApplySoftDeleteQueryFilter(modelBuilder, entityType);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AggregateRoot> aggregateRoots = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        IReadOnlyList<IDomainEvent> domainEvents = aggregateRoots
            .SelectMany(aggregateRoot => aggregateRoot.DomainEvents)
            .ToList();

        foreach (AggregateRoot aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
        }

        int result = await base.SaveChangesAsync(cancellationToken);

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            object notification = Activator.CreateInstance(notificationType, domainEvent)!;

            await _publisher.Publish(notification, cancellationToken);
        }

        return result;
    }

    private static void IgnoreDomainEvents(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        if (!typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
        {
            return;
        }

        modelBuilder.Entity(entityType.ClrType).Ignore(nameof(AggregateRoot.DomainEvents));
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
        {
            return;
        }

        ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "entity");
        MemberExpression property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        UnaryExpression not = Expression.Not(property);
        LambdaExpression lambda = Expression.Lambda(not, parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }
}
