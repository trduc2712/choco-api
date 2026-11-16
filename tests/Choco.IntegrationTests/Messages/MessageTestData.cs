using System.Net.Http.Headers;
using System.Net.Http.Json;

using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Messages;

internal static class MessageTestData
{
    public static async Task<Message> AddMessageAsync(
        ChocoApiFactory factory, User sender, User recipient, Guid clientMessageId, string content = "content")
    {
        DateTimeOffset now = factory.TimeProvider.GetUtcNow();
        Conversation conversation = Conversation.Create(sender.Id, recipient.Id, now).Value;
        Message message = conversation.SendMessage(sender.Id, clientMessageId, content, now);

        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(conversation);
            db.Add(message);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return message;
    }

    public static HttpRequestMessage SendRequest(Guid recipientId, object body, string identityId = "identity-id")
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/users/{recipientId}/messages")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));
        return request;
    }

    public static Task<List<Conversation>> GetConversationsAsync(ChocoApiFactory factory)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<Conversation>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    public static Task<List<Message>> GetMessagesAsync(ChocoApiFactory factory)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<Message>().AsNoTracking()
            .OrderBy(message => message.ConversationId)
            .ThenBy(message => message.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
