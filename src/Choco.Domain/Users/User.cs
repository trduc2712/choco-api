using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Users;

public sealed class User : AggregateRoot, IAuditable, ISoftDeletable
{
    public const int IdentityIdMaxLength = 64;
    public const int UsernameMaxLength = 64;
    public const int DisplayNameMaxLength = 256;
    public const int AvatarUrlMaxLength = 2048;

    private readonly List<UserRole> _roles = [];

    public string IdentityId { get; private set; } = null!;
    public string Username { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private User() { }

    private User(Guid id, string identityId, string username, string displayName, string? avatarUrl)
        : base(id)
    {
        IdentityId = identityId;
        Username = username;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
    }

    public static User Create(string identityId, string username, string? displayName, string? avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        return new User(
            Guid.NewGuid(),
            identityId,
            username,
            string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            avatarUrl);
    }

    public void AssignRole(Guid roleId, Guid? assignedBy, DateTimeOffset now)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(roleId));
        }

        if (HoldsRole(roleId))
        {
            return;
        }

        _roles.Add(UserRole.Create(roleId, now, assignedBy));
    }

    public void RevokeRole(Guid roleId)
    {
        _roles.RemoveAll(role => role.RoleId == roleId);
    }

    public bool HoldsRole(Guid roleId)
    {
        return _roles.Any(role => role.RoleId == roleId);
    }
}
