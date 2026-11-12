namespace Choco.Contract.Reports;

/// <summary>The user that open reports are about.</summary>
/// <param name="Username">The user's handle.</param>
/// <param name="DisplayName">The name shown to other people.</param>
/// <param name="IsSuspended">Whether an administrator already suspended the user.</param>
/// <example>{"username":"username","displayName":"Display Name","isSuspended":false}</example>
public sealed record ReportedUserResponse(string Username, string DisplayName, bool IsSuspended);
