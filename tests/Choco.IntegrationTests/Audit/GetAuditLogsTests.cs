using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Audit;

public sealed class GetAuditLogsTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    private const string Path = "/api/v1/admin/audit-logs";

    private static readonly DateTimeOffset Time = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TargetId = new("7a3d9c2b-8e1f-4b6a-a5d4-1c0e9f8b7a21");
    private static readonly Guid OtherTargetId = new("2b8e4d6f-1a3c-4e9b-8f7d-6c5a4b3e2d10");

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username");
        await AddEntryAsync(Guid.NewGuid(), Time);
        using HttpRequestMessage request = Request(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotReadAuditLog_ReturnsForbidden()
    {
        Role role = Role.CreateSystem(new SystemRoleDefinition(
            "reviewer-key", "Reviewer", IsDefault: false, Permissions: [Permission.ReadAnyFeedback]));
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(role);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = Request(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Administrator_ListsEntriesNewestFirstWithUsernamesRolesAndDetail()
    {
        await AddAdministratorAsync();
        User actor = await AddUserAsync("identity-id-2", "username-2");
        User affected = await AddUserAsync("identity-id-3", "username-3");
        await AddEntryAsync(actor.Id, Time, AuditAction.PostEdited, AuditTargetType.Post, TargetId);
        await AddEntryAsync(
            actor.Id, Time.AddMinutes(1), AuditAction.RoleAssigned, AuditTargetType.User, affected.Id, affected.Id, "Role: Administrator");

        JsonElement body = await GetJsonAsync(Path);

        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("action").GetString()).Should().Equal("RoleAssigned", "PostEdited");
        JsonElement newest = items[0];
        newest.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(Time.AddMinutes(1));
        newest.GetProperty("actorType").GetString().Should().Be("User");
        newest.GetProperty("actor").GetProperty("id").GetGuid().Should().Be(actor.Id);
        newest.GetProperty("actor").GetProperty("username").GetString().Should().Be("username-2");
        newest.GetProperty("actorRoles").EnumerateArray().Select(role => role.GetString()).Should().Equal("Administrator", "Member");
        newest.GetProperty("clientId").GetString().Should().Be("https://client.example.com");
        newest.GetProperty("targetType").GetString().Should().Be("User");
        newest.GetProperty("targetId").GetGuid().Should().Be(affected.Id);
        newest.GetProperty("affectedUser").GetProperty("username").GetString().Should().Be("username-3");
        newest.GetProperty("detail").GetString().Should().Be("Role: Administrator");
        newest.GetProperty("traceId").GetString().Should().Be("trace-id");
        items[1].GetProperty("affectedUser").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Entries_DoNotExposeTheSourceAddressOrTheUserAgent()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time);

        using HttpRequestMessage request = Request(HttpMethod.Get, Path);
        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().NotContainEquivalentOf("ipAddress").And.NotContainEquivalentOf("userAgent").And.NotContain("192.0.2.1");
    }

    [Fact]
    public async Task EntriesOfADeletedUser_StillShowTheUsername()
    {
        await AddAdministratorAsync();
        User actor = await AddUserAsync("identity-id-2", "username-2");
        await AddEntryAsync(actor.Id, Time);
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == actor.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        JsonElement body = await GetJsonAsync(Path);

        body.GetProperty("items")[0].GetProperty("actor").GetProperty("username").GetString().Should().Be("username-2");
    }

    [Fact]
    public async Task EntryOfASystemActor_HasNoActor()
    {
        await AddAdministratorAsync();
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(AuditLogEntry.Create(
                Time, AuditActorType.System, null, [], null, AuditAction.RoleAssigned, AuditTargetType.User, TargetId, null, null, null, null, null));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        JsonElement body = await GetJsonAsync(Path);

        JsonElement item = body.GetProperty("items")[0];
        item.GetProperty("actorType").GetString().Should().Be("System");
        item.GetProperty("actor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Entries_AreReturnedByPageWithoutOverlapAndWithTheTotal()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time, targetId: TargetId);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1), targetId: TargetId);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(2), targetId: TargetId);

        JsonElement first = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2");
        JsonElement second = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=2");

        first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time.AddMinutes(2), Time.AddMinutes(1));
        first.GetProperty("page").GetInt32().Should().Be(1);
        first.GetProperty("pageSize").GetInt32().Should().Be(2);
        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time);
        second.GetProperty("page").GetInt32().Should().Be(2);
        second.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Entries_WithoutQuery_UseTheFirstPageOfTwentyAndCamelCaseNames()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time);

        JsonElement body = await GetJsonAsync(Path);

        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "items", "page", "pageSize", "totalCount");
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Entries_ReturnTheMiddleAndLastPagesAndAnEmptyPagePastTheEnd()
    {
        await AddAdministratorAsync();
        for (int index = 0; index < 5; index++)
        {
            await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(index));
        }

        JsonElement middle = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=2");
        JsonElement last = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=3");
        JsonElement pastEnd = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=99");
        JsonElement hugePage = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=50&page={int.MaxValue}");

        middle.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time.AddMinutes(2), Time.AddMinutes(1));
        last.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time);
        pastEnd.GetProperty("items").GetArrayLength().Should().Be(0);
        pastEnd.GetProperty("page").GetInt32().Should().Be(99);
        pastEnd.GetProperty("totalCount").GetInt32().Should().Be(5);
        hugePage.GetProperty("items").GetArrayLength().Should().Be(0);
        hugePage.GetProperty("totalCount").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Entries_AcceptTheSmallestAndLargestPageSize()
    {
        await AddAdministratorAsync();
        for (int index = 0; index < 3; index++)
        {
            await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(index));
        }

        JsonElement smallest = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=1");
        JsonElement largest = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=50");

        smallest.GetProperty("items").GetArrayLength().Should().Be(1);
        smallest.GetProperty("totalCount").GetInt32().Should().Be(3);
        largest.GetProperty("items").GetArrayLength().Should().Be(3);
        largest.GetProperty("pageSize").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task Entries_AtTheSameInstant_AreOrderedByIdDescendingOnEveryPage()
    {
        await AddAdministratorAsync();
        List<Guid> ids = [];
        for (int index = 0; index < 5; index++)
        {
            ids.Add(await AddEntryAsync(Guid.NewGuid(), Time));
        }

        List<Guid> expected = ids.OrderDescending().ToList();

        JsonElement first = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=1");
        JsonElement second = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=2");
        JsonElement third = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=3");
        JsonElement repeated = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2&page=1");

        static IEnumerable<Guid> Ids(JsonElement body) => body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

        Ids(first).Concat(Ids(second)).Concat(Ids(third)).Should().Equal(expected);
        Ids(repeated).Should().Equal(Ids(first));
    }

    [Fact]
    public async Task TotalCount_CountsAllMatchesAcrossPagesWhenFiltered()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time, AuditAction.PostDeleted);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1), AuditAction.PostDeleted);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(2), AuditAction.PostDeleted);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(3), AuditAction.PostEdited);

        JsonElement body = await GetJsonAsync($"{Path}?action=PostDeleted&pageSize=2");

        body.GetProperty("items").GetArrayLength().Should().Be(2);
        body.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Entries_AreFilteredByActor()
    {
        await AddAdministratorAsync();
        User actor = await AddUserAsync("identity-id-2", "username-2");
        await AddEntryAsync(actor.Id, Time);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1));

        JsonElement body = await GetJsonAsync($"{Path}?actorId={actor.Id}");

        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("actor").GetProperty("id").GetGuid().Should().Be(actor.Id);
    }

    [Fact]
    public async Task Entries_AreFilteredByAction()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time, AuditAction.PostEdited);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1), AuditAction.PostDeleted);

        JsonElement body = await GetJsonAsync($"{Path}?action=PostDeleted");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("action").GetString())
            .Should().Equal("PostDeleted");
    }

    [Fact]
    public async Task Entries_AreFilteredByTargetTypeAndTargetId()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time, targetType: AuditTargetType.Post, targetId: TargetId);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1), targetType: AuditTargetType.Comment, targetId: TargetId);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(2), targetType: AuditTargetType.Post, targetId: OtherTargetId);

        JsonElement body = await GetJsonAsync($"{Path}?targetType=Post&targetId={TargetId}");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time);
    }

    [Fact]
    public async Task Entries_AreFilteredByAffectedUser()
    {
        await AddAdministratorAsync();
        User affected = await AddUserAsync("identity-id-2", "username-2");
        await AddEntryAsync(Guid.NewGuid(), Time, affectedUserId: affected.Id);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1));

        JsonElement body = await GetJsonAsync($"{Path}?affectedUserId={affected.Id}");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time);
    }

    [Fact]
    public async Task Entries_AreFilteredByATimeRangeThatIncludesFromAndExcludesTo()
    {
        await AddAdministratorAsync();
        await AddEntryAsync(Guid.NewGuid(), Time);
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(1));
        await AddEntryAsync(Guid.NewGuid(), Time.AddMinutes(2));

        string from = Uri.EscapeDataString(Time.AddMinutes(1).ToString("O"));
        string to = Uri.EscapeDataString(Time.AddMinutes(2).ToString("O"));
        JsonElement body = await GetJsonAsync($"{Path}?from={from}&to={to}");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("occurredAt").GetDateTimeOffset())
            .Should().Equal(Time.AddMinutes(1));
    }

    [Theory]
    [InlineData("action=Unknown", "action")]
    [InlineData("targetType=Unknown", "targetType")]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    public async Task InvalidParameter_ReturnsValidationProblem(string query, string key)
    {
        await AddAdministratorAsync();
        using HttpRequestMessage request = Request(HttpMethod.Get, $"{Path}?{query}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(key);
    }

    private async Task<User> AddAdministratorAsync()
    {
        return await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
    }

    private async Task<Guid> AddEntryAsync(
        Guid actorId,
        DateTimeOffset occurredAt,
        AuditAction action = AuditAction.PostDeleted,
        AuditTargetType targetType = AuditTargetType.Post,
        Guid? targetId = null,
        Guid? affectedUserId = null,
        string? detail = null)
    {
        AuditLogEntry entry = AuditLogEntry.Create(
            occurredAt,
            AuditActorType.User,
            actorId,
            ["Administrator", "Member"],
            "https://client.example.com",
            action,
            targetType,
            targetId ?? TargetId,
            affectedUserId,
            detail,
            "trace-id",
            "user-agent",
            System.Net.IPAddress.Parse("192.0.2.1"));

        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(entry);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return entry.Id;
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken));
    }
}
