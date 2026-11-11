using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Reports a post to the administrators.</summary>
/// <param name="PostId">The reported post's id; the route sets it.</param>
/// <param name="Reason">Why the post is reported: Spam, Harassment, HateSpeech, Violence or Other.</param>
/// <param name="Details">An optional explanation, up to 500 characters.</param>
/// <example>{"reason":"Spam","details":"details"}</example>
public sealed record ReportPostCommand(Guid PostId, string Reason, string? Details) : ICommand;
