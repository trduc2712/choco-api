using System.Net;
using System.Net.Http.Json;

using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Audit;

public sealed class SystemActorAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    [Fact]
    public async Task CreateUser_RecordsUserCreatedWithTheSystemAsActor()
    {
        Result result = await SendAsync(new CreateUserCommand("identity-id", "username", "Display Name", null));

        result.IsSuccess.Should().BeTrue();
        User user = await GetUserByIdentityIdAsync("identity-id");
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeSystemEntry(entry, AuditAction.UserCreated, user.Id, user.Id);
    }

    [Fact]
    public async Task CreateUser_WhenTheUserAlreadyExists_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");

        Result result = await SendAsync(new CreateUserCommand("identity-id", "username-2", "Display Name", null));

        result.IsSuccess.Should().BeTrue();
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task FirstRequestOfANewUser_RecordsUserCreatedBySystemAndTheRequestByTheUser()
    {
        Factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username",
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/posts", new { content = "content" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        User user = await GetUserByIdentityIdAsync("identity-id");
        List<AuditLogEntry> entries = await ReadEntriesAsync();
        entries.Should().HaveCount(2);
        ShouldBeSystemEntry(entries.Single(entry => entry.Action == AuditAction.UserCreated), AuditAction.UserCreated, user.Id, user.Id);
        AuditLogEntry created = entries.Single(entry => entry.Action == AuditAction.PostCreated);
        created.ActorType.Should().Be(AuditActorType.User);
        created.ActorId.Should().Be(user.Id);
    }

    [Fact]
    public async Task BootstrapAdministrators_RecordsRoleAssignedForEachUserWhoReceivedTheRole()
    {
        User user = await AddUserAsync(ChocoApiFactory.BootstrapAdminIdentityId, "username");
        await AddUserAsync("identity-id", "username-2");

        Result result = await SendAsync(new BootstrapAdministratorsCommand());

        result.IsSuccess.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeSystemEntry(entry, AuditAction.RoleAssigned, user.Id, user.Id, "Role: Administrator");
    }

    [Fact]
    public async Task BootstrapAdministrators_WhenTheUserAlreadyHoldsTheRole_RecordsNoEntry()
    {
        await AddUserAsync(ChocoApiFactory.BootstrapAdminIdentityId, "username", await GetAdministratorRoleIdAsync());

        Result result = await SendAsync(new BootstrapAdministratorsCommand());

        result.IsSuccess.Should().BeTrue();
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task BootstrapAdministrators_WhenNoListedUserExists_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");

        Result result = await SendAsync(new BootstrapAdministratorsCommand());

        result.IsSuccess.Should().BeTrue();
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    private static void ShouldBeSystemEntry(
        AuditLogEntry entry, AuditAction action, Guid targetId, Guid? affectedUserId, string? detail = null)
    {
        entry.ActorType.Should().Be(AuditActorType.System);
        entry.ActorId.Should().BeNull();
        entry.ActorRoles.Should().BeEmpty();
        entry.OccurredAt.Should().Be(ChocoApiFactory.StartTime);
        entry.Action.Should().Be(action);
        entry.TargetType.Should().Be(AuditTargetType.User);
        entry.TargetId.Should().Be(targetId);
        entry.AffectedUserId.Should().Be(affectedUserId);
        entry.Detail.Should().Be(detail);
    }

    private async Task<Result> SendAsync(IRequest<Result> command)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(command, TestContext.Current.CancellationToken);
    }

    private Task<User> GetUserByIdentityIdAsync(string identityId)
    {
        return Factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .AsNoTracking()
            .SingleAsync(user => user.IdentityId == identityId, TestContext.Current.CancellationToken));
    }
}
