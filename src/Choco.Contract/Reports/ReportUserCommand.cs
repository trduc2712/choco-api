using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Reports a user to the administrators.</summary>
/// <param name="UserId">The reported user's id; the route sets it.</param>
/// <param name="Reason">Why the user is reported: Spam, Harassment, HateSpeech, Violence or Other.</param>
/// <param name="Details">An optional explanation, up to 500 characters.</param>
/// <example>{"reason":"Harassment","details":"details"}</example>
public sealed record ReportUserCommand(Guid UserId, string Reason, string? Details) : ICommand;
