using Choco.Domain.Conversations;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", table => table.HasComment("Text messages that users send in direct conversations."));

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.ConversationId)
            .HasComment("Conversation the message belongs to.");

        builder.Property(message => message.Sequence)
            .HasComment("Position of the message in its conversation, starting at 1 and growing by one per message.");

        builder.Property(message => message.SenderId)
            .HasComment("User who sent the message.");

        builder.Property(message => message.ClientMessageId)
            .HasComment("Identifier the sender's client created for the send; a repeated send with it returns the same message.");

        builder.Property(message => message.Content)
            .HasMaxLength(Message.ContentMaxLength)
            .HasComment("Message text as the sender wrote it, without leading and trailing whitespace.");

        builder.Property(message => message.SentAt)
            .HasComment("When the message was saved.");

        builder.HasIndex(message => new { message.ConversationId, message.Sequence }).IsUnique();
        builder.HasIndex(message => new { message.ConversationId, message.SenderId, message.ClientMessageId }).IsUnique();

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
