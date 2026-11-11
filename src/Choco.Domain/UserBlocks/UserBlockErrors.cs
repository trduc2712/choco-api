using Choco.SharedKernel.Results;

namespace Choco.Domain.UserBlocks;

public static class UserBlockErrors
{
    public static readonly Error SelfBlock = Error.Validation("UserBlock.SelfBlock", "Users cannot block themselves.");
    public static readonly Error Blocked = Error.Forbidden("UserBlock.Blocked", "A block between these users prevents this action.");
}
