using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Gets one piece of feedback.</summary>
/// <param name="Id">The feedback's id.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record GetFeedbackByIdQuery(Guid Id) : IQuery<FeedbackResponse>;
