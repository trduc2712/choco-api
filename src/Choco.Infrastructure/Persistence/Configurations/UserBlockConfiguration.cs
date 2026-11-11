using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("user_blocks", table => table.HasComment("Active blocks between users."));
        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).ValueGeneratedNever();
        builder.Property(block => block.BlockerId).HasComment("User who blocked the other user.");
        builder.Property(block => block.BlockedUserId).HasComment("User who was blocked.");
        builder.Property(block => block.BlockedAt).HasComment("When the user blocked the other user.");

        builder.HasIndex(block => new { block.BlockerId, block.BlockedUserId }).IsUnique();
        builder.HasIndex(block => block.BlockedUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
