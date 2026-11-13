namespace Choco.Domain.AuditLogs;

public sealed record AuditChange(
    string Field,
    string? Before = null,
    string? After = null,
    IReadOnlyList<string>? Added = null,
    IReadOnlyList<string>? Removed = null);
