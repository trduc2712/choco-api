using System.Runtime.CompilerServices;

using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Auditing;

internal sealed class AuditChanges
{
    private readonly ConditionalWeakTable<Result, IReadOnlyList<AuditChange>> _changes = new();

    public TResult Record<TResult>(TResult result, IReadOnlyList<AuditChange> changes) where TResult : Result
    {
        _changes.Add(result, changes);
        return result;
    }

    public IReadOnlyList<AuditChange>? Get(Result result)
    {
        return _changes.TryGetValue(result, out IReadOnlyList<AuditChange>? changes) ? changes : null;
    }

    public static void Field<T>(List<AuditChange> changes, string field, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
        {
            changes.Add(new AuditChange(field));
        }
    }

    public static AuditChange Suspension(bool suspended)
    {
        return suspended
            ? new AuditChange("isSuspended", "false", "true")
            : new AuditChange("isSuspended", "true", "false");
    }
}
