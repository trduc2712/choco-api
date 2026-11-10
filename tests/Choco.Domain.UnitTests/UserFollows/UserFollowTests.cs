using Choco.Domain.UserFollows;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.UserFollows;

public sealed class UserFollowTests
{
    private static readonly Guid FollowerId = new("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset FollowedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithDifferentUsers_ReturnsFollow()
    {
        Guid followedUserId = new("00000000-0000-0000-0000-000000000002");

        Result<UserFollow> result = UserFollow.Create(FollowerId, followedUserId, FollowedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.FollowerId.Should().Be(FollowerId);
        result.Value.FollowedUserId.Should().Be(followedUserId);
        result.Value.FollowedAt.Should().Be(FollowedAt);
    }

    [Fact]
    public void Create_WithDifferentUsers_RaisesUserFollowed()
    {
        Guid followedUserId = new("00000000-0000-0000-0000-000000000002");

        Result<UserFollow> result = UserFollow.Create(FollowerId, followedUserId, FollowedAt);

        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new UserFollowed(FollowerId, followedUserId, FollowedAt));
    }

    [Fact]
    public void Create_WithSameUser_ReturnsSelfFollowError()
    {
        Result<UserFollow> result = UserFollow.Create(FollowerId, FollowerId, FollowedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserFollowErrors.SelfFollow);
    }
}
