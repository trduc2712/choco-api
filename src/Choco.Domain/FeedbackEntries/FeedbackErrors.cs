using Choco.SharedKernel.Results;

namespace Choco.Domain.FeedbackEntries;

public static class FeedbackErrors
{
    public static readonly Error NotFound = Error.NotFound("Feedback.NotFound", "The feedback was not found.");

    public static readonly Error CannotReturnToNew = Error.Conflict(
        "Feedback.CannotReturnToNew", "Feedback that is no longer new cannot go back to new.");
}
