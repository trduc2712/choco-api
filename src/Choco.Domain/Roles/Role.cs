using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Roles;

public sealed class Role : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 64;
    public const int SystemKeyMaxLength = 64;

    private readonly List<string> _permissions = [];

    public string Name { get; private set; } = null!;
    public string? SystemKey { get; private set; }
    public bool IsDefault { get; private set; }
    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }

    private Role() { }

    private Role(Guid id, string name, string? systemKey, bool isDefault, IEnumerable<string> permissions)
        : base(id)
    {
        Name = name;
        SystemKey = systemKey;
        IsDefault = isDefault;
        _permissions.AddRange(permissions);
    }

    public static Role CreateSystem(SystemRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Key, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Key.Length, SystemKeyMaxLength, nameof(definition));
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Name.Length, NameMaxLength, nameof(definition));

        EnsureKnown(definition.Permissions, nameof(definition));

        return new Role(
            Guid.NewGuid(),
            definition.Name,
            definition.Key,
            definition.IsDefault,
            definition.Permissions.Distinct(StringComparer.Ordinal));
    }

    public static Role Create(string name, IEnumerable<string> permissions)
    {
        string trimmedName = Normalize(name, permissions);

        return new Role(Guid.NewGuid(), trimmedName, null, false, permissions.Distinct(StringComparer.Ordinal));
    }

    public void Update(string name, IEnumerable<string> permissions)
    {
        string trimmedName = Normalize(name, permissions);
        List<string> distinctPermissions = permissions.Distinct(StringComparer.Ordinal).ToList();

        Name = trimmedName;
        _permissions.Clear();
        _permissions.AddRange(distinctPermissions);
    }

    public Result CheckCanBeChangedBy(
        IReadOnlyCollection<string> actorPermissions, bool actorHoldsRole, IEnumerable<string> newPermissions)
    {
        ArgumentNullException.ThrowIfNull(actorPermissions);
        ArgumentNullException.ThrowIfNull(newPermissions);

        if (actorHoldsRole)
        {
            return Result.Failure(RoleErrors.HeldByCaller);
        }

        if (!IsCoveredBy(actorPermissions) || !newPermissions.All(actorPermissions.Contains))
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        return Result.Success();
    }

    public Result CheckCanBeDeleted()
    {
        if (IsDefault)
        {
            return Result.Failure(RoleErrors.DefaultNotDeletable);
        }

        return SystemKey is null ? Result.Success() : Result.Failure(RoleErrors.SystemNotDeletable);
    }

    public bool IsCoveredBy(IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return _permissions.All(permissions.Contains);
    }

    private static string Normalize(string name, IEnumerable<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);

        string trimmedName = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedName.Length, NameMaxLength, nameof(name));
        EnsureKnown(permissions, nameof(permissions));

        return trimmedName;
    }

    private static void EnsureKnown(IEnumerable<string> permissions, string paramName)
    {
        string? unknownPermission = permissions.FirstOrDefault(permission => !Permission.All.Contains(permission));

        if (unknownPermission is not null)
        {
            throw new ArgumentException($"Unknown permission '{unknownPermission}'.", paramName);
        }
    }
}
