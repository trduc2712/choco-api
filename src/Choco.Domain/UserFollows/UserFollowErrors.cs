using Choco.SharedKernel.Results;

namespace Choco.Domain.UserFollows;

public static class UserFollowErrors
{
    public static readonly Error SelfFollow = Error.Validation("UserFollow.SelfFollow", "Users cannot follow themselves.");
}
