using Choco.SharedKernel.Results;

namespace Choco.Domain.FeedbackEntries;

public static class FeedbackErrors
{
    public static readonly Error NotFound = Error.NotFound("Feedback.NotFound", "The feedback was not found.");
}
