using System.Text.Json.Serialization;

namespace Choco.Contract.AuditLogs;

/// <summary>A field changed by the audited action.</summary>
/// <param name="Field">The field name. For personal data and content, this is the only property returned.</param>
/// <param name="Before">The previous scalar value as a string, when recorded.</param>
/// <param name="After">The resulting scalar value as a string, when recorded.</param>
/// <param name="Added">The permission codes added to the role, when permissions are recorded.</param>
/// <param name="Removed">The permission codes removed from the role, when permissions are recorded.</param>
/// <example>{"field":"name","before":"Role Name","after":"Updated Role Name"}</example>
public sealed record AuditChangeResponse(
    string Field,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Before,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? After,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Added,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Removed);
