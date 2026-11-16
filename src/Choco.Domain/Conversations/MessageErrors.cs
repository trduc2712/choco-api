using Choco.SharedKernel.Results;

namespace Choco.Domain.Conversations;

public static class MessageErrors
{
    public static readonly Error NotFound = Error.NotFound("Message.NotFound", "The message was not found.");
}
