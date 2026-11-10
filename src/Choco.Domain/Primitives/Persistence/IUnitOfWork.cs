namespace Choco.Domain.Primitives.Persistence;

public interface IUnitOfWork
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
    Task AfterCommitAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
