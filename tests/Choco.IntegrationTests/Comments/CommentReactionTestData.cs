using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Comments;

internal static class CommentReactionTestData
{
    public static Task<int> AddReactionsAsync(
        ChocoApiFactory factory, params (Comment Comment, User User, CommentReactionType Type)[] reactions)
    {
        DateTimeOffset now = factory.TimeProvider.GetUtcNow();
        List<CommentReaction> added = reactions
            .Select(reaction => CommentReaction.Create(reaction.Comment.Id, reaction.User.Id, reaction.Type, now))
            .ToList();
        added.ForEach(reaction => reaction.ClearDomainEvents());

        return factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(added);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static Task<List<CommentReaction>> GetReactionsAsync(ChocoApiFactory factory, Guid commentId)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<CommentReaction>().AsNoTracking()
            .Where(reaction => reaction.CommentId == commentId)
            .OrderBy(reaction => reaction.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
