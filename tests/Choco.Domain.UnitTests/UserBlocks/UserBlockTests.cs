using Choco.Domain.UserBlocks;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.UserBlocks;

public sealed class UserBlockTests
{
    private static readonly Guid BlockerId = new("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset BlockedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithDifferentUsers_ReturnsBlock()
    {
        Guid blockedUserId = new("00000000-0000-0000-0000-000000000002");

        Result<UserBlock> result = UserBlock.Create(BlockerId, blockedUserId, BlockedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.BlockerId.Should().Be(BlockerId);
        result.Value.BlockedUserId.Should().Be(blockedUserId);
        result.Value.BlockedAt.Should().Be(BlockedAt);
    }

    [Fact]
    public void Create_WithSameUser_ReturnsSelfBlockError()
    {
        Result<UserBlock> result = UserBlock.Create(BlockerId, BlockerId, BlockedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserBlockErrors.SelfBlock);
    }
}
