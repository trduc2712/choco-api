using System.Text.Json.Serialization;

namespace Choco.Contract.AuditLogs;

/// <summary>A field changed by the audited action.</summary>
/// <param name="Field">The field name. For personal data and content, this is the only property returned.</param>
/// <param name="Before">The previous scalar value as a string, when recorded.</param>
/// <param name="After">The resulting scalar value as a string, when recorded.</param>
/// <param name="Added">The permission codes added to the role, or the role names given to a user, when they are recorded.</param>
/// <param name="Removed">The permission codes removed from the role, or the role names taken from a user, when they are recorded.</param>
/// <param name="PreviousChangedAt">When the same field of the same target last changed before this entry, in UTC. Only in the single-entry response, and left out when it never changed before.</param>
/// <example>{"field":"status","before":"wont_fix","after":"resolved","previousChangedAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record AuditChangeResponse(
    string Field,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Before,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? After,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Added,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Removed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? PreviousChangedAt = null);
