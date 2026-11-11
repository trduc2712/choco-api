using Choco.Domain.PostLikes;

namespace Choco.Domain.UnitTests.PostLikes;

public sealed class PostLikeTests
{
    [Fact]
    public void Create_SetsPostUserAndTime()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000001");
        Guid userId = new("00000000-0000-0000-0000-000000000002");
        DateTimeOffset likedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostLike like = PostLike.Create(postId, userId, likedAt);

        like.Id.Should().NotBeEmpty();
        like.PostId.Should().Be(postId);
        like.UserId.Should().Be(userId);
        like.LikedAt.Should().Be(likedAt);
    }

    [Fact]
    public void Create_RaisesPostLiked()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000001");
        Guid userId = new("00000000-0000-0000-0000-000000000002");
        DateTimeOffset likedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostLike like = PostLike.Create(postId, userId, likedAt);

        like.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new PostLiked(postId, userId, likedAt));
    }

    [Fact]
    public void Create_Twice_GivesDifferentIds()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000001");
        Guid userId = new("00000000-0000-0000-0000-000000000002");
        DateTimeOffset likedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostLike first = PostLike.Create(postId, userId, likedAt);
        PostLike second = PostLike.Create(postId, userId, likedAt);

        first.Id.Should().NotBe(second.Id);
    }
}
