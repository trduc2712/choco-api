using Choco.Domain.Primitives.Events;

using MediatR;

namespace Choco.Application.Abstractions;

public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
