using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.AdminCounts;

/// <summary>Gets the counts the admin sidebar shows.</summary>
/// <example>{}</example>
public sealed record GetAdminCountsQuery : IQuery<AdminCountsResponse>;
