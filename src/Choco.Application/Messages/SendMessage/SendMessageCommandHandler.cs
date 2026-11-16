using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messages;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using Microsoft.Extensions.Logging;

namespace Choco.Application.Messages.SendMessage;

internal sealed class SendMessageCommandHandler(
    ICurrentUser currentUser,
    IUserContext userContext,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IMessageRealtimePublisher realtimePublisher,
    ILogger<SendMessageCommandHandler> logger) : ICommandHandler<SendMessageCommand, MessageResponse>
{
    public async Task<Result<MessageResponse>> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<MessageResponse>.Failure(caller.Error);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Result<Conversation> candidate = Conversation.Create(caller.Value, command.RecipientId, now);
        if (candidate.IsFailure)
        {
            return Result<MessageResponse>.Failure(candidate.Error);
        }

        User? recipient = await userRepository.GetByIdForReadAsync(command.RecipientId, cancellationToken);
        if (recipient is null)
        {
            return Result<MessageResponse>.Failure(UserErrors.NotFound);
        }

        if (await blockRepository.ExistsBetweenForReadAsync(caller.Value, recipient.Id, cancellationToken))
        {
            return Result<MessageResponse>.Failure(UserBlockErrors.Blocked);
        }

        MessageResponse message = await unitOfWork.InTransactionAsync(async () =>
        {
            await conversationRepository.AddIfMissingAsync(candidate.Value, cancellationToken);

            Conversation conversation = await conversationRepository.GetBetweenForUpdateAsync(
                    caller.Value, recipient.Id, cancellationToken)
                ?? throw new InvalidOperationException("The conversation is missing right after it was added.");

            Message? saved = await messageRepository.GetByClientMessageIdForReadAsync(
                conversation.Id, caller.Value, command.ClientMessageId, cancellationToken);
            if (saved is not null)
            {
                return saved.ToResponse();
            }

            Message sent = conversation.SendMessage(caller.Value, command.ClientMessageId, command.Content, now);
            messageRepository.Add(sent);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            MessageResponse response = sent.ToResponse();
            await unitOfWork.AfterCommitAsync(async afterCommitToken =>
            {
                try
                {
                    await realtimePublisher.PublishAsync(
                        userContext.IdentityId, recipient.IdentityId, response, afterCommitToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Failed to deliver message {MessageId} in real time.", response.Id);
                }
            }, cancellationToken);

            return response;
        }, cancellationToken);

        return Result<MessageResponse>.Success(message);
    }
}
