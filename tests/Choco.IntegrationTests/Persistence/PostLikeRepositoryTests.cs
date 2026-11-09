using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class PostLikeRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public PostLikeRepositoryTests(ChocoApiFactory factory)
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

    [Fact]
    public async Task Summaries_CountEachPostSeparatelyAndFlagTheViewersLikes()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post likedByBoth = await AddPostAsync(other);
        Post likedByOther = await AddPostAsync(other);
        Post unliked = await AddPostAsync(other);
        await AddLikesAsync((likedByBoth, viewer), (likedByBoth, other), (likedByOther, other));

        IReadOnlyDictionary<Guid, PostLikeSummary> summaries = await GetSummariesAsync(
            [likedByBoth.Id, likedByOther.Id, unliked.Id], viewer.Id);

        summaries[likedByBoth.Id].Should().Be(new PostLikeSummary(2, true));
        summaries[likedByOther.Id].Should().Be(new PostLikeSummary(1, false));
        summaries.Should().NotContainKey(unliked.Id);
    }

    [Fact]
    public async Task Summaries_SkipLikesOfDeletedUsersAndOtherPosts()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User deleted = await AddUserAsync("identity-id-2", "username-2");
        Post asked = await AddPostAsync(viewer);
        Post notAsked = await AddPostAsync(viewer);
        await AddLikesAsync((asked, viewer), (asked, deleted), (notAsked, viewer));
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == deleted.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        IReadOnlyDictionary<Guid, PostLikeSummary> summaries = await GetSummariesAsync([asked.Id], viewer.Id);

        summaries.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<Guid, PostLikeSummary>(asked.Id, new PostLikeSummary(1, true)));
    }

    [Fact]
    public async Task Summaries_WithoutPostIds_ReturnNothing()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        await AddLikesAsync((await AddPostAsync(viewer), viewer));

        IReadOnlyDictionary<Guid, PostLikeSummary> summaries = await GetSummariesAsync([], viewer.Id);

        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task AddIfMissing_WithAnExistingLike_KeepsOneLike()
    {
        User user = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(user);
        DateTimeOffset firstLikedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await AddIfMissingAsync(PostLike.Create(post.Id, user.Id, firstLikedAt));

        await AddIfMissingAsync(PostLike.Create(post.Id, user.Id, firstLikedAt.AddHours(1)));

        PostLike like = (await GetLikesAsync(post.Id)).Should().ContainSingle().Subject;
        like.LikedAt.Should().Be(firstLikedAt);
    }

    [Fact]
    public async Task RemoveIfPresent_RemovesOnlyTheGivenPairAndIsRepeatable()
    {
        User first = await AddUserAsync("identity-id", "username");
        User second = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(first);
        Post otherPost = await AddPostAsync(first);
        await AddLikesAsync((post, first), (post, second), (otherPost, first));

        await RemoveIfPresentAsync(post.Id, first.Id);
        await RemoveIfPresentAsync(post.Id, first.Id);

        (await GetLikesAsync(post.Id)).Should().ContainSingle().Which.UserId.Should().Be(second.Id);
        (await GetLikesAsync(otherPost.Id)).Should().ContainSingle().Which.UserId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Likes_OfTheSameUserAndPost_ViolateTheUniqueIndex()
    {
        User user = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(user);
        DateTimeOffset likedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await AddLikesAsync((post, user));

        Func<Task> addDuplicate = () => _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(PostLike.Create(post.Id, user.Id, likedAt));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        (await addDuplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ix_post_likes_post_id_user_id");
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

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private Task<int> AddLikesAsync(params (Post Post, User User)[] likes)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            dbContext.AddRange(likes.Select(like => PostLike.Create(like.Post.Id, like.User.Id, now)));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<List<PostLike>> GetLikesAsync(Guid postId)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<PostLike>().AsNoTracking()
            .Where(like => like.PostId == postId)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private async Task AddIfMissingAsync(PostLike like)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostLikeRepository repository = scope.ServiceProvider.GetRequiredService<IPostLikeRepository>();

        await repository.AddIfMissingAsync(like, TestContext.Current.CancellationToken);
    }

    private async Task RemoveIfPresentAsync(Guid postId, Guid userId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostLikeRepository repository = scope.ServiceProvider.GetRequiredService<IPostLikeRepository>();

        await repository.RemoveIfPresentAsync(postId, userId, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, PostLikeSummary>> GetSummariesAsync(Guid[] postIds, Guid viewerId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostLikeRepository repository = scope.ServiceProvider.GetRequiredService<IPostLikeRepository>();

        return await repository.GetSummariesForReadAsync(postIds, viewerId, TestContext.Current.CancellationToken);
    }
}
