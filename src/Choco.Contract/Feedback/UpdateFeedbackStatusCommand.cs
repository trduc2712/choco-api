using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Changes the triage state of a piece of feedback.</summary>
/// <param name="Id">The feedback's id; the route sets it.</param>
/// <param name="Status">The new state: new, in_progress, resolved or wont_fix.</param>
/// <example>{"status":"in_progress"}</example>
public sealed record UpdateFeedbackStatusCommand(Guid Id, string Status) : ICommand<FeedbackResponse>;
