using Choco.Domain.CommentReactions;

namespace Choco.Application.Comments;

internal static class CommentReactionTypes
{
    public const string Options = "like, dislike";

    public static string ToWire(CommentReactionType type)
    {
        return type switch
        {
            CommentReactionType.Like => "like",
            CommentReactionType.Dislike => "dislike",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public static bool TryParse(string? value, out CommentReactionType type)
    {
        type = default;

        switch (value)
        {
            case "like":
                type = CommentReactionType.Like;
                return true;
            case "dislike":
                type = CommentReactionType.Dislike;
                return true;
            default:
                return false;
        }
    }
}
