using Choco.Domain.Conversations;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.Conversations;

public sealed class ConversationTests
{
    private static readonly Guid UserId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OutsiderId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ClientMessageId = new("4b6f2c1a-8d3e-4f5a-9b7c-1e2d3f4a5b6c");
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SentAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(ParticipantOrders))]
    public void Create_WithTwoUsers_ReturnsConversationWithUsersInFixedOrder(Guid userId, Guid otherUserId)
    {
        Result<Conversation> result = Conversation.Create(userId, otherUserId, CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.FirstUserId.Should().Be(UserId);
        result.Value.SecondUserId.Should().Be(OtherUserId);
        result.Value.FirstUserLastReadSequence.Should().Be(0);
        result.Value.SecondUserLastReadSequence.Should().Be(0);
        result.Value.LastMessageSequence.Should().Be(0);
        result.Value.LastActivityAt.Should().Be(CreatedAt);
        result.Value.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithSameUser_ReturnsSelfConversationError()
    {
        Result<Conversation> result = Conversation.Create(UserId, UserId, CreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ConversationErrors.SelfConversation);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        Func<Result<Conversation>> act = () => Conversation.Create(Guid.Empty, OtherUserId, CreatedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void Create_WithEmptyOtherUserId_ThrowsArgumentException()
    {
        Func<Result<Conversation>> act = () => Conversation.Create(UserId, Guid.Empty, CreatedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("otherUserId");
    }

    [Theory]
    [MemberData(nameof(ParticipantOrders))]
    public void OrderParticipants_InEitherOrder_ReturnsSameOrder(Guid userId, Guid otherUserId)
    {
        (Guid FirstUserId, Guid SecondUserId) participants = Conversation.OrderParticipants(userId, otherUserId);

        participants.Should().Be((UserId, OtherUserId));
    }

    [Theory]
    [MemberData(nameof(ParticipantChecks))]
    public void IsParticipant_ForUser_ReturnsWhetherUserTakesPart(Guid userId, bool expected)
    {
        Conversation conversation = CreateConversation();

        bool isParticipant = conversation.IsParticipant(userId);

        isParticipant.Should().Be(expected);
    }

    [Fact]
    public void SendMessage_FirstMessage_ReturnsMessageWithSequenceOne()
    {
        Conversation conversation = CreateConversation();

        Message message = conversation.SendMessage(UserId, ClientMessageId, "content", SentAt);

        message.Id.Should().NotBeEmpty();
        message.ConversationId.Should().Be(conversation.Id);
        message.Sequence.Should().Be(1);
        message.SenderId.Should().Be(UserId);
        message.ClientMessageId.Should().Be(ClientMessageId);
        message.Content.Should().Be("content");
        message.SentAt.Should().Be(SentAt);
    }

    [Fact]
    public void SendMessage_FirstMessage_RecordsLastMessageAndSenderReadPosition()
    {
        Conversation conversation = CreateConversation();

        conversation.SendMessage(OtherUserId, ClientMessageId, "content", SentAt);

        conversation.LastMessageSequence.Should().Be(1);
        conversation.LastActivityAt.Should().Be(SentAt);
        conversation.FirstUserLastReadSequence.Should().Be(0);
        conversation.SecondUserLastReadSequence.Should().Be(1);
    }

    [Fact]
    public void SendMessage_AfterEarlierMessages_ReturnsNextSequence()
    {
        Conversation conversation = CreateConversation();
        conversation.SendMessage(UserId, new Guid("00000000-0000-0000-0000-00000000000a"), "content-1", CreatedAt);
        conversation.SendMessage(OtherUserId, new Guid("00000000-0000-0000-0000-00000000000b"), "content-2", CreatedAt);

        Message message = conversation.SendMessage(UserId, ClientMessageId, "content-3", SentAt);

        message.Sequence.Should().Be(3);
        conversation.LastMessageSequence.Should().Be(3);
    }

    [Fact]
    public void SendMessage_WithSurroundingWhitespace_StoresContentTrimmedAtBothEnds()
    {
        Conversation conversation = CreateConversation();

        Message message = conversation.SendMessage(
            UserId, ClientMessageId, " \n\tcontent-line-1\n\n  content-line-2 \t\n", SentAt);

        message.Content.Should().Be("content-line-1\n\n  content-line-2");
    }

    [Fact]
    public void SendMessage_WithContentAtMaxLengthAfterTrimming_ReturnsMessageWithTrimmedContent()
    {
        Conversation conversation = CreateConversation();
        string trimmedContent = new('c', Message.ContentMaxLength);

        Message message = conversation.SendMessage(UserId, ClientMessageId, $"  {trimmedContent}  ", SentAt);

        message.Content.Should().Be(trimmedContent);
    }

    [Fact]
    public void SendMessage_WithContentOverMaxLength_ThrowsAndLeavesConversationUnchanged()
    {
        Conversation conversation = CreateConversation();
        string content = new('c', Message.ContentMaxLength + 1);

        Func<Message> act = () => conversation.SendMessage(UserId, ClientMessageId, content, SentAt);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
        conversation.LastMessageSequence.Should().Be(0);
        conversation.LastActivityAt.Should().Be(CreatedAt);
        conversation.FirstUserLastReadSequence.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SendMessage_WithBlankContent_ThrowsArgumentException(string? content)
    {
        Conversation conversation = CreateConversation();

        Func<Message> act = () => conversation.SendMessage(UserId, ClientMessageId, content!, SentAt);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void SendMessage_WithEmptyClientMessageId_ThrowsArgumentException()
    {
        Conversation conversation = CreateConversation();

        Func<Message> act = () => conversation.SendMessage(UserId, Guid.Empty, "content", SentAt);

        act.Should().Throw<ArgumentException>().WithParameterName("clientMessageId");
    }

    [Fact]
    public void SendMessage_FromOutsider_ThrowsArgumentException()
    {
        Conversation conversation = CreateConversation();

        Func<Message> act = () => conversation.SendMessage(OutsiderId, ClientMessageId, "content", SentAt);

        act.Should().Throw<ArgumentException>().WithParameterName("senderId");
    }

    [Fact]
    public void MarkRead_UpToEarlierMessage_MovesReadPositionThere()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();

        conversation.MarkRead(UserId, 2);

        conversation.LastReadSequenceOf(UserId).Should().Be(2);
        conversation.UnreadCountFor(UserId).Should().Be(1);
    }

    [Fact]
    public void MarkRead_PastLastMessage_StopsAtLastMessage()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();

        conversation.MarkRead(UserId, 4);

        conversation.LastReadSequenceOf(UserId).Should().Be(3);
        conversation.UnreadCountFor(UserId).Should().Be(0);
    }

    [Fact]
    public void MarkRead_BeforeCurrentReadPosition_KeepsReadPosition()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();
        conversation.MarkRead(UserId, 2);

        conversation.MarkRead(UserId, 1);

        conversation.LastReadSequenceOf(UserId).Should().Be(2);
    }

    [Fact]
    public void MarkRead_ByOneUser_LeavesOtherUserReadPosition()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();

        conversation.MarkRead(UserId, 1);

        conversation.FirstUserLastReadSequence.Should().Be(1);
        conversation.SecondUserLastReadSequence.Should().Be(3);
    }

    [Fact]
    public void MarkRead_WithNegativeSequence_ThrowsArgumentException()
    {
        Conversation conversation = CreateConversation();

        Action act = () => conversation.MarkRead(UserId, -1);

        act.Should().Throw<ArgumentException>().WithParameterName("sequence");
    }

    [Fact]
    public void MarkRead_ByOutsider_ThrowsArgumentException()
    {
        Conversation conversation = CreateConversation();

        Action act = () => conversation.MarkRead(OutsiderId, 0);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void UnreadCountFor_MessagesFromOtherUser_CountsThem()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();

        long unreadCount = conversation.UnreadCountFor(UserId);

        unreadCount.Should().Be(3);
    }

    [Fact]
    public void UnreadCountFor_Sender_IsZero()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();

        long unreadCount = conversation.UnreadCountFor(OtherUserId);

        unreadCount.Should().Be(0);
    }

    [Fact]
    public void UnreadCountFor_AfterReplying_IsZero()
    {
        Conversation conversation = CreateConversationWithMessagesFromOtherUser();
        conversation.SendMessage(UserId, ClientMessageId, "content", SentAt);

        long unreadCount = conversation.UnreadCountFor(UserId);

        unreadCount.Should().Be(0);
    }

    [Fact]
    public void UnreadCountFor_Outsider_ThrowsArgumentException()
    {
        Conversation conversation = CreateConversation();

        Func<long> act = () => conversation.UnreadCountFor(OutsiderId);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    public static TheoryData<Guid, Guid> ParticipantOrders => new()
    {
        { UserId, OtherUserId },
        { OtherUserId, UserId }
    };

    public static TheoryData<Guid, bool> ParticipantChecks => new()
    {
        { UserId, true },
        { OtherUserId, true },
        { OutsiderId, false }
    };

    private static Conversation CreateConversation()
    {
        return Conversation.Create(UserId, OtherUserId, CreatedAt).Value;
    }

    private static Conversation CreateConversationWithMessagesFromOtherUser()
    {
        Conversation conversation = CreateConversation();
        conversation.SendMessage(OtherUserId, new Guid("00000000-0000-0000-0000-00000000000a"), "content-1", CreatedAt);
        conversation.SendMessage(OtherUserId, new Guid("00000000-0000-0000-0000-00000000000b"), "content-2", CreatedAt);
        conversation.SendMessage(OtherUserId, new Guid("00000000-0000-0000-0000-00000000000c"), "content-3", CreatedAt);

        return conversation;
    }
}
