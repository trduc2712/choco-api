using Choco.Domain.Conversations;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", table => table.HasComment("Direct conversations between two users."));

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .ValueGeneratedNever();

        builder.Property(conversation => conversation.FirstUserId)
            .HasComment("One of the two users; the pair is stored in a fixed order so that two users have one conversation.");

        builder.Property(conversation => conversation.SecondUserId)
            .HasComment("The other of the two users.");

        builder.Property(conversation => conversation.FirstUserLastReadSequence)
            .HasComment("Sequence number of the last message the first user has read; 0 when none.");

        builder.Property(conversation => conversation.SecondUserLastReadSequence)
            .HasComment("Sequence number of the last message the second user has read; 0 when none.");

        builder.Property(conversation => conversation.LastMessageSequence)
            .HasComment("Sequence number of the newest message; 0 when there is none.");

        builder.Property(conversation => conversation.LastActivityAt)
            .HasComment("When the newest message was sent, or when the conversation was created if it has none.");

        builder.HasIndex(conversation => new { conversation.FirstUserId, conversation.SecondUserId }).IsUnique();
        builder.HasIndex(conversation => conversation.SecondUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.FirstUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.SecondUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
