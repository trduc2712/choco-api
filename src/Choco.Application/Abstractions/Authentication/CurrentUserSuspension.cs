namespace Choco.Application.Abstractions.Authentication;

public sealed record CurrentUserSuspension(string Reason, DateTimeOffset SuspendedAt);
