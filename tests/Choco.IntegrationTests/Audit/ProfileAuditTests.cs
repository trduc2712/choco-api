using System.Net;
using System.Net.Http.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class ProfileAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    private const string ProfilePath = "/api/v1/me/profile";
    private const string AvatarPath = ProfilePath + "/avatar";
    private const string AvatarUrl = "https://example.com/avatar.png";

    private static readonly byte[] Image = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");

    [Fact]
    public async Task UpdateMyProfile_RecordsProfileUpdatedWithoutTheNewValues()
    {
        User user = await AddUserAsync("identity-id", "username");
        Factory.IdentityProvider.RespondWith(_ => ProviderProfile("username-updated", "First Name", "Last Name", AvatarUrl));
        using HttpRequestMessage request = Request(
            HttpMethod.Put, ProfilePath, new { username = "username-updated", firstName = "First Name", lastName = "Last Name" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, user.Id, AuditAction.ProfileUpdated, AuditTargetType.User, user.Id);
    }

    [Fact]
    public async Task UpdateMyBio_RecordsBioUpdatedWithoutTheBio()
    {
        User user = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Put, ProfilePath + "/bio", new { bio = "bio" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, user.Id, AuditAction.BioUpdated, AuditTargetType.User, user.Id);
    }

    [Fact]
    public async Task UploadMyAvatar_RecordsAvatarUploaded()
    {
        User user = await AddUserAsync("identity-id", "username");
        Factory.IdentityProvider.RespondWith(_ => ProviderProfile("username", "Display Name", null, AvatarUrl));
        using HttpRequestMessage request = Request(HttpMethod.Post, AvatarPath);
        MultipartFormDataContent body = new();
        body.Add(new ByteArrayContent(Image), "image", "avatar.png");
        request.Content = body;

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, user.Id, AuditAction.AvatarUploaded, AuditTargetType.User, user.Id);
    }

    [Fact]
    public async Task UploadMyAvatar_WhenTheProviderRejectsTheImage_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        Factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errors = new[] { new { code = "request_body_invalid" } } })
        });
        using HttpRequestMessage request = Request(HttpMethod.Post, AvatarPath);
        MultipartFormDataContent body = new();
        body.Add(new ByteArrayContent(Image), "image", "avatar.png");
        request.Content = body;

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMyAvatar_RecordsAvatarRemoved()
    {
        User user = await AddUserAsync("identity-id", "username");
        Factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { })
        });
        using HttpRequestMessage request = Request(HttpMethod.Delete, AvatarPath);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, user.Id, AuditAction.AvatarRemoved, AuditTargetType.User, user.Id);
    }

    private static HttpResponseMessage ProviderProfile(string username, string firstName, string? lastName, string avatarUrl) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username,
                first_name = firstName,
                last_name = lastName,
                image_url = avatarUrl
            })
        };
}
