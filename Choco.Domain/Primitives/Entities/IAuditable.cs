namespace Choco.Domain.Primitives.Entities;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset ModifiedAt { get; }
}
