using Choco.Application.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Users;

public sealed class UserFollowPagesTests
{
    private static readonly DateTimeOffset FollowedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlySet<Guid> NoFollowed = new HashSet<Guid>();

    [Fact]
    public void ReadPosition_WithoutCursor_ReturnsNull()
    {
        (DateTimeOffset FollowedAt, Guid FollowId)? position = UserFollowPages.ReadPosition(null);

        position.Should().BeNull();
    }

    [Fact]
    public void ReadPosition_WithValidCursor_ReturnsItsPosition()
    {
        Guid followId = new("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f");
        string cursor = new FollowCursor(FollowedAt, followId).Encode();

        (DateTimeOffset FollowedAt, Guid FollowId)? position = UserFollowPages.ReadPosition(cursor);

        position.Should().Be((FollowedAt, followId));
    }

    [Fact]
    public void ToPage_WithoutEntries_ReturnsEmptyPageWithoutNextCursor()
    {
        var page = UserFollowPages.ToPage([], 2, NoFollowed);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ToPage_WithAtMostLimitEntries_ReturnsAllWithoutNextCursor(int count)
    {
        IReadOnlyList<UserFollowEntry> entries = Entries(count);

        var page = UserFollowPages.ToPage(entries, 2, NoFollowed);

        page.Items.Select(item => item.Username).Should().Equal(entries.Select(entry => entry.User.Username));
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitEntries_ReturnsLimitEntriesAndCursorOfTheLastReturned()
    {
        IReadOnlyList<UserFollowEntry> entries = Entries(3);

        var page = UserFollowPages.ToPage(entries, 2, NoFollowed);

        page.Items.Select(item => item.Username).Should().Equal(entries[0].User.Username, entries[1].User.Username);
        FollowCursor.TryDecode(page.NextCursor, out FollowCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new FollowCursor(entries[1].FollowedAt, entries[1].FollowId));
    }

    [Fact]
    public void ToPage_MarksOnlyTheFollowedUsers()
    {
        IReadOnlyList<UserFollowEntry> entries = Entries(3);
        HashSet<Guid> followed = [entries[0].User.Id, entries[2].User.Id];

        var page = UserFollowPages.ToPage(entries, 10, followed);

        page.Items.Select(item => item.IsFollowedByMe).Should().Equal(true, false, true);
    }

    [Fact]
    public void PageUserIds_ReturnsTheIdsOfTheReturnedEntriesOnly()
    {
        IReadOnlyList<UserFollowEntry> entries = Entries(3);

        Guid[] ids = UserFollowPages.PageUserIds(entries, 2);

        ids.Should().Equal(entries[0].User.Id, entries[1].User.Id);
    }

    [Fact]
    public void ToPage_MapsUsernameDisplayNameAndAvatarUrl()
    {
        User user = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        UserFollowEntry entry = new(new Guid("00000000-0000-0000-0000-000000000001"), FollowedAt, user);

        var page = UserFollowPages.ToPage([entry], 1, NoFollowed);

        page.Items.Should().ContainSingle().Which.Should().Be(
            new Choco.Contract.Users.UserSearchItemResponse("username", "Display Name", "https://example.com/avatar.png", false));
    }

    private static List<UserFollowEntry> Entries(int count)
    {
        return Enumerable.Range(1, count)
            .Select(number => new UserFollowEntry(
                new Guid(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
                FollowedAt.AddHours(-number),
                User.Create($"identity-id-{number}", $"username-{number}", "Display Name", null)))
            .ToList();
    }
}
