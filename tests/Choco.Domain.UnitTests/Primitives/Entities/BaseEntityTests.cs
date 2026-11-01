using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.UnitTests.Primitives.Entities;

public sealed class BaseEntityTests
{
    [Fact]
    public void Equals_WithSameTypeAndId_ReturnsTrue()
    {
        Guid id = new("00000000-0000-0000-0000-000000000001");
        TestEntity entity = new(id);
        TestEntity other = new(id);

        bool equal = entity.Equals(other);

        equal.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        TestEntity entity = new(new Guid("00000000-0000-0000-0000-000000000001"));
        TestEntity other = new(new Guid("00000000-0000-0000-0000-000000000002"));

        bool equal = entity.Equals(other);

        equal.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentTypeAndSameId_ReturnsFalse()
    {
        Guid id = new("00000000-0000-0000-0000-000000000001");
        TestEntity entity = new(id);
        OtherTestEntity other = new(id);

        bool equal = entity.Equals(other);

        equal.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBothIdsEmpty_ReturnsFalse()
    {
        TestEntity entity = new(Guid.Empty);
        TestEntity other = new(Guid.Empty);

        bool equal = entity.Equals(other);

        equal.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameInstanceAndEmptyId_ReturnsTrue()
    {
        TestEntity entity = new(Guid.Empty);

        bool equal = entity.Equals(entity);

        equal.Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameTypeAndId_ReturnsSameValue()
    {
        Guid id = new("00000000-0000-0000-0000-000000000001");
        TestEntity entity = new(id);
        TestEntity other = new(id);

        int hashCode = entity.GetHashCode();

        hashCode.Should().Be(other.GetHashCode());
    }

    private sealed class TestEntity : BaseEntity
    {
        public TestEntity(Guid id) : base(id) { }
    }

    private sealed class OtherTestEntity : BaseEntity
    {
        public OtherTestEntity(Guid id) : base(id) { }
    }
}
