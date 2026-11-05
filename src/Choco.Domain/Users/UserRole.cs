using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Users;

public sealed class UserRole : BaseEntity
{
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    private UserRole() { }

    private UserRole(Guid id, Guid roleId, DateTimeOffset assignedAt, Guid? assignedBy)
        : base(id)
    {
        RoleId = roleId;
        AssignedAt = assignedAt;
        AssignedBy = assignedBy;
    }

    internal static UserRole Create(Guid roleId, DateTimeOffset assignedAt, Guid? assignedBy)
    {
        return new UserRole(Guid.NewGuid(), roleId, assignedAt, assignedBy);
    }
}
