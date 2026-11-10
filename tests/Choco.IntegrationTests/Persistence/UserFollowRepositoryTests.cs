using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class UserFollowRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset FirstFollowedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ChocoApiFactory _factory;

    public UserFollowRepositoryTests(ChocoApiFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task List_ReturnsMostRecentFollowFirst(string list)
    {
        User target = await AddUserAsync("identity-id", "username");
        User first = await AddUserAsync("identity-id-2", "username-2");
        User second = await AddUserAsync("identity-id-3", "username-3");
        User third = await AddUserAsync("identity-id-4", "username-4");
        await AddFollowAsync(list, target, second, 2);
        await AddFollowAsync(list, target, third, 3);
        await AddFollowAsync(list, target, first, 1);

        IReadOnlyList<UserFollowEntry> entries = await ListAsync(list, target.Id, null, 10);

        entries.Select(entry => entry.User.Id).Should().Equal(third.Id, second.Id, first.Id);
        entries.Select(entry => entry.FollowedAt).Should().Equal(
            FirstFollowedAt.AddHours(3), FirstFollowedAt.AddHours(2), FirstFollowedAt.AddHours(1));
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task List_SkipsDeletedUsersAndOtherUsersFollows(string list)
    {
        User target = await AddUserAsync("identity-id", "username");
        User kept = await AddUserAsync("identity-id-2", "username-2");
        User deleted = await AddUserAsync("identity-id-3", "username-3");
        User other = await AddUserAsync("identity-id-4", "username-4");
        await AddFollowAsync(list, target, kept, 1);
        await AddFollowAsync(list, target, deleted, 2);
        await AddFollowAsync(list, other, kept, 3);
        await DeleteUserAsync(deleted.Id);

        IReadOnlyList<UserFollowEntry> entries = await ListAsync(list, target.Id, null, 10);

        entries.Select(entry => entry.User.Id).Should().Equal(kept.Id);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task List_WithPosition_ReturnsOnlyOlderFollows(string list)
    {
        User target = await AddUserAsync("identity-id", "username");
        User first = await AddUserAsync("identity-id-2", "username-2");
        User second = await AddUserAsync("identity-id-3", "username-3");
        User third = await AddUserAsync("identity-id-4", "username-4");
        await AddFollowAsync(list, target, first, 1);
        UserFollow secondFollow = await AddFollowAsync(list, target, second, 2);
        await AddFollowAsync(list, target, third, 3);

        IReadOnlyList<UserFollowEntry> entries = await ListAsync(
            list, target.Id, (secondFollow.FollowedAt, secondFollow.Id), 10);

        entries.Select(entry => entry.User.Id).Should().Equal(first.Id);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task List_ReturnsAtMostCount(string list)
    {
        User target = await AddUserAsync("identity-id", "username");
        User first = await AddUserAsync("identity-id-2", "username-2");
        User second = await AddUserAsync("identity-id-3", "username-3");
        User third = await AddUserAsync("identity-id-4", "username-4");
        await AddFollowAsync(list, target, first, 1);
        await AddFollowAsync(list, target, second, 2);
        await AddFollowAsync(list, target, third, 3);

        IReadOnlyList<UserFollowEntry> entries = await ListAsync(list, target.Id, null, 2);

        entries.Select(entry => entry.User.Id).Should().Equal(third.Id, second.Id);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task List_WithSameFollowedAt_OrdersByFollowIdAndPagesWithoutGaps(string list)
    {
        User target = await AddUserAsync("identity-id", "username");
        User first = await AddUserAsync("identity-id-2", "username-2");
        User second = await AddUserAsync("identity-id-3", "username-3");
        UserFollow firstFollow = await AddFollowAsync(list, target, first, 1);
        UserFollow secondFollow = await AddFollowAsync(list, target, second, 1);
        Guid[] expectedFollowOrder = new[] { firstFollow.Id, secondFollow.Id }
            .OrderByDescending(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<UserFollowEntry> firstPage = await ListAsync(list, target.Id, null, 1);

        IReadOnlyList<UserFollowEntry> secondPage = await ListAsync(
            list, target.Id, (firstPage[0].FollowedAt, firstPage[0].FollowId), 1);

        firstPage.Select(entry => entry.FollowId).Should().Equal(expectedFollowOrder[0]);
        secondPage.Select(entry => entry.FollowId).Should().Equal(expectedFollowOrder[1]);
    }

    [Fact]
    public async Task FollowedUserIds_ReturnOnlyTheAskedUsersTheFollowerFollows()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User followedAsked = await AddUserAsync("identity-id-2", "username-2");
        User notFollowedAsked = await AddUserAsync("identity-id-3", "username-3");
        User followedNotAsked = await AddUserAsync("identity-id-4", "username-4");
        User otherFollower = await AddUserAsync("identity-id-5", "username-5");
        await AddFollowAsync("following", follower, followedAsked, 1);
        await AddFollowAsync("following", follower, followedNotAsked, 2);
        await AddFollowAsync("following", otherFollower, notFollowedAsked, 3);
        await AddFollowAsync("followers", follower, otherFollower, 4);

        IReadOnlySet<Guid> ids = await GetFollowedUserIdsAsync(
            follower.Id, [followedAsked.Id, notFollowedAsked.Id, otherFollower.Id]);

        ids.Should().BeEquivalentTo([followedAsked.Id]);
    }

    [Fact]
    public async Task FollowedUserIds_WithoutAskedUsers_ReturnNothing()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User followed = await AddUserAsync("identity-id-2", "username-2");
        await AddFollowAsync("following", follower, followed, 1);

        IReadOnlySet<Guid> ids = await GetFollowedUserIdsAsync(follower.Id, []);

        ids.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_CountsEachDirectionSeparatelyAndSkipsDeletedUsers()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        User follower = await AddUserAsync("identity-id-3", "username-3");
        User followed = await AddUserAsync("identity-id-4", "username-4");
        User deletedFollower = await AddUserAsync("identity-id-5", "username-5");
        User deletedFollowed = await AddUserAsync("identity-id-6", "username-6");
        await AddFollowAsync("followers", target, viewer, 1);
        await AddFollowAsync("followers", target, follower, 2);
        await AddFollowAsync("followers", target, deletedFollower, 3);
        await AddFollowAsync("following", target, followed, 4);
        await AddFollowAsync("following", target, deletedFollowed, 5);
        await DeleteUserAsync(deletedFollower.Id);
        await DeleteUserAsync(deletedFollowed.Id);

        UserFollowSummary summary = await GetSummaryAsync(target.Id, viewer.Id);

        summary.Should().Be(new UserFollowSummary(true, 2, 1));
    }

    [Fact]
    public async Task Summary_ForViewerWhoOnlyIsFollowedByTarget_IsNotFollowedByViewer()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        await AddFollowAsync("following", target, viewer, 1);

        UserFollowSummary summary = await GetSummaryAsync(target.Id, viewer.Id);

        summary.Should().Be(new UserFollowSummary(false, 0, 1));
    }

    [Fact]
    public async Task Summary_WithoutRelationships_IsEmpty()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");

        UserFollowSummary summary = await GetSummaryAsync(target.Id, viewer.Id);

        summary.Should().Be(new UserFollowSummary(false, 0, 0));
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<UserFollow> AddFollowAsync(string list, User target, User other, int hours)
    {
        Guid followerId = list == "followers" ? other.Id : target.Id;
        Guid followedUserId = list == "followers" ? target.Id : other.Id;
        UserFollow follow = UserFollow.Create(followerId, followedUserId, FirstFollowedAt.AddHours(hours)).Value;
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(follow);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return follow;
    }

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<IReadOnlyList<UserFollowEntry>> ListAsync(
        string list, Guid userId, (DateTimeOffset FollowedAt, Guid FollowId)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserFollowRepository repository = scope.ServiceProvider.GetRequiredService<IUserFollowRepository>();

        return list == "followers"
            ? await repository.ListFollowersForReadAsync(userId, after, count, TestContext.Current.CancellationToken)
            : await repository.ListFollowingForReadAsync(userId, after, count, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlySet<Guid>> GetFollowedUserIdsAsync(Guid followerId, Guid[] userIds)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserFollowRepository repository = scope.ServiceProvider.GetRequiredService<IUserFollowRepository>();

        return await repository.GetFollowedUserIdsForReadAsync(followerId, userIds, TestContext.Current.CancellationToken);
    }

    private async Task<UserFollowSummary> GetSummaryAsync(Guid userId, Guid viewerId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserFollowRepository repository = scope.ServiceProvider.GetRequiredService<IUserFollowRepository>();

        return await repository.GetSummaryForReadAsync(userId, viewerId, TestContext.Current.CancellationToken);
    }
}
