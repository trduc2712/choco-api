using Choco.Domain.CommentReactions;

namespace Choco.Domain.UnitTests.CommentReactions;

public sealed class CommentReactionTests
{
    private static readonly Guid CommentId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = new("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset ReactedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChangedAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsCommentUserTypeAndTime()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Dislike, ReactedAt);

        reaction.Id.Should().NotBeEmpty();
        reaction.CommentId.Should().Be(CommentId);
        reaction.UserId.Should().Be(UserId);
        reaction.Type.Should().Be(CommentReactionType.Dislike);
        reaction.ReactedAt.Should().Be(ReactedAt);
    }

    [Fact]
    public void Create_WithLike_RaisesCommentLiked()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Like, ReactedAt);

        reaction.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new CommentLiked(CommentId, UserId, ReactedAt));
    }

    [Fact]
    public void Create_WithDislike_RaisesNoEvent()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Dislike, ReactedAt);

        reaction.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyCommentId_ThrowsArgumentException()
    {
        Action act = () => CommentReaction.Create(Guid.Empty, UserId, CommentReactionType.Like, ReactedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("commentId");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        Action act = () => CommentReaction.Create(CommentId, Guid.Empty, CommentReactionType.Like, ReactedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void Create_WithUndefinedType_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => CommentReaction.Create(CommentId, UserId, (CommentReactionType)99, ReactedAt);

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("type");
    }

    [Fact]
    public void ChangeTo_OtherType_ChangesTypeAndTime()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Like, ReactedAt);

        reaction.ChangeTo(CommentReactionType.Dislike, ChangedAt);

        reaction.Type.Should().Be(CommentReactionType.Dislike);
        reaction.ReactedAt.Should().Be(ChangedAt);
    }

    [Fact]
    public void ChangeTo_LikeFromDislike_RaisesCommentLiked()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Dislike, ReactedAt);
        reaction.ClearDomainEvents();

        reaction.ChangeTo(CommentReactionType.Like, ChangedAt);

        reaction.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new CommentLiked(CommentId, UserId, ChangedAt));
    }

    [Fact]
    public void ChangeTo_DislikeFromLike_RaisesNoEvent()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Like, ReactedAt);
        reaction.ClearDomainEvents();

        reaction.ChangeTo(CommentReactionType.Dislike, ChangedAt);

        reaction.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeTo_SameType_KeepsTheReactionAndRaisesNoEvent()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Like, ReactedAt);
        reaction.ClearDomainEvents();

        reaction.ChangeTo(CommentReactionType.Like, ChangedAt);

        reaction.Type.Should().Be(CommentReactionType.Like);
        reaction.ReactedAt.Should().Be(ReactedAt);
        reaction.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeTo_UndefinedType_ThrowsAndKeepsTheReaction()
    {
        CommentReaction reaction = CommentReaction.Create(CommentId, UserId, CommentReactionType.Like, ReactedAt);

        Action act = () => reaction.ChangeTo((CommentReactionType)99, ChangedAt);

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("type");
        reaction.Type.Should().Be(CommentReactionType.Like);
        reaction.ReactedAt.Should().Be(ReactedAt);
    }
}
